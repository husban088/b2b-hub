import { gql } from 'apollo-angular';

export const LOGIN = gql`
  mutation Login($input: LoginInput!) {
    login(input: $input) {
      token
      user { id fullName email role }
    }
  }
`;

export const DASHBOARD_SUMMARY = gql`
  query DashboardSummary {
    dashboardSummary {
      totalPartners
      activePartners
      totalIntegrations
      connectedIntegrations
      failingIntegrations
      recentEvents
    }
  }
`;

export const GET_PARTNERS = gql`
  query Partners {
    partners {
      id companyName companyEmail country industry logoUrl status createdAt updatedAt
    }
  }
`;

export const CREATE_PARTNER = gql`
  mutation CreatePartner($input: CreatePartnerInput!) {
    createPartner(input: $input) {
      id companyName companyEmail country industry status createdAt updatedAt
    }
  }
`;

export const UPDATE_PARTNER = gql`
  mutation UpdatePartner($input: UpdatePartnerInput!) {
    updatePartner(input: $input) {
      id companyName country industry status updatedAt
    }
  }
`;

export const DELETE_PARTNER = gql`
  mutation DeletePartner($id: String!) {
    deletePartner(id: $id)
  }
`;

export const GET_INTEGRATIONS = gql`
  query Integrations {
    integrations {
      id partnerId name type status endpointUrl scopes lastSyncAt createdAt updatedAt
    }
  }
`;

export const CREATE_INTEGRATION = gql`
  mutation CreateIntegration($input: CreateIntegrationInput!) {
    createIntegration(input: $input) {
      id partnerId name type status endpointUrl scopes createdAt updatedAt
    }
  }
`;

export const UPDATE_INTEGRATION_STATUS = gql`
  mutation UpdateIntegrationStatus($id: String!, $status: IntegrationStatus!) {
    updateIntegrationStatus(id: $id, status: $status) {
      id status lastSyncAt updatedAt
    }
  }
`;

export const DELETE_INTEGRATION = gql`
  mutation DeleteIntegration($id: String!) {
    deleteIntegration(id: $id)
  }
`;

export const GET_WEBHOOK_LOGS = gql`
  query WebhookLogs($integrationId: String, $limit: Int!) {
    webhookLogs(integrationId: $integrationId, limit: $limit) {
      id integrationId direction eventType statusCode success payload errorMessage receivedAt
    }
  }
`;

export const ON_WEBHOOK_EVENT = gql`
  subscription OnWebhookEvent {
    onWebhookEvent {
      id integrationId direction eventType statusCode success payload errorMessage receivedAt
    }
  }
`;
