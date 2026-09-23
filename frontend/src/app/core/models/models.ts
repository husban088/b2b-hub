export type PartnerStatus = 'PENDING' | 'ACTIVE' | 'SUSPENDED' | 'OFFBOARDED';
export type IntegrationType = 'REST_API' | 'GRAPHQL' | 'WEBHOOK' | 'FILE_SYNC' | 'EDI';
export type IntegrationStatus = 'DRAFT' | 'CONNECTED' | 'FAILING' | 'PAUSED';
export type UserRole = 'ADMIN' | 'OPERATOR' | 'VIEWER';

export interface Partner {
  id: string;
  companyName: string;
  companyEmail: string;
  country: string;
  industry: string;
  logoUrl?: string | null;
  status: PartnerStatus;
  createdAt: string;
  updatedAt: string;
}

export interface Integration {
  id: string;
  partnerId: string;
  name: string;
  type: IntegrationType;
  status: IntegrationStatus;
  endpointUrl: string;
  scopes: string[];
  lastSyncAt?: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface WebhookLog {
  id: string;
  integrationId: string;
  direction: string;
  eventType: string;
  statusCode: number;
  success: boolean;
  payload?: string | null;
  errorMessage?: string | null;
  receivedAt: string;
}

export interface DashboardSummary {
  totalPartners: number;
  activePartners: number;
  totalIntegrations: number;
  connectedIntegrations: number;
  failingIntegrations: number;
  recentEvents: number;
}

export interface AppUser {
  id: string;
  fullName: string;
  email: string;
  role: UserRole;
}

export interface AuthPayload {
  token: string;
  user: AppUser;
}
