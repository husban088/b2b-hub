export const environment = {
  production: true,
  // Replace with your deployed AWS backend URL (ECS/App Runner behind ALB, API Gateway, etc.)
  graphqlHttpUrl: 'https://api.your-domain.com/graphql',
  graphqlWsUrl: 'wss://api.your-domain.com/graphql'
};
