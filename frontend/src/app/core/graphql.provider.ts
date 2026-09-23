import { APP_INITIALIZER } from '@angular/core';
import { HttpLink } from 'apollo-angular/http';
import { ApolloClientOptions, InMemoryCache, split, ApolloLink } from '@apollo/client/core';
import { setContext } from '@apollo/client/link/context';
import { GraphQLWsLink } from '@apollo/client/link/subscriptions';
import { getMainDefinition } from '@apollo/client/utilities';
import { createClient } from 'graphql-ws';
import { Apollo } from 'apollo-angular';
import { environment } from '../../environments/environment';

const AUTH_TOKEN_KEY = 'nexbridge_token';

export function readAuthToken(): string | null {
  return localStorage.getItem(AUTH_TOKEN_KEY);
}

export function storeAuthToken(token: string): void {
  localStorage.setItem(AUTH_TOKEN_KEY, token);
}

export function clearAuthToken(): void {
  localStorage.removeItem(AUTH_TOKEN_KEY);
}

/**
 * Builds the Apollo client: HTTP link for queries/mutations, WS link for
 * live subscriptions (webhook activity feed), split by operation type,
 * with the JWT attached to every request.
 */
export function createApollo(httpLink: HttpLink): ApolloClientOptions<unknown> {
  const authLink = setContext((_, { headers }) => {
    const token = readAuthToken();
    return {
      headers: {
        ...headers,
        ...(token ? { Authorization: `Bearer ${token}` } : {})
      }
    };
  });

  const http = authLink.concat(httpLink.create({ uri: environment.graphqlHttpUrl }));

  const wsLink = new GraphQLWsLink(
    createClient({
      url: environment.graphqlWsUrl,
      connectionParams: () => {
        const token = readAuthToken();
        return token ? { Authorization: `Bearer ${token}` } : {};
      }
    })
  );

  const link = split(
    ({ query }) => {
      const definition = getMainDefinition(query);
      return definition.kind === 'OperationDefinition' && definition.operation === 'subscription';
    },
    wsLink,
    http as unknown as ApolloLink
  );

  return {
    link,
    cache: new InMemoryCache()
  };
}
