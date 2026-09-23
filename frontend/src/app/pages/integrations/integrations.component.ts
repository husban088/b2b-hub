import { Component, OnInit, signal } from "@angular/core";
import { CommonModule } from "@angular/common";
import { FormsModule } from "@angular/forms";
import { Apollo } from "apollo-angular";
import { firstValueFrom } from "rxjs";
import {
  GET_INTEGRATIONS,
  GET_PARTNERS,
  CREATE_INTEGRATION,
  UPDATE_INTEGRATION_STATUS,
  DELETE_INTEGRATION,
} from "../../core/graphql.operations";
import {
  Integration,
  IntegrationStatus,
  IntegrationType,
  Partner,
} from "../../core/models/models";
import { ConfirmService } from "../../core/services/confirm.service";

@Component({
  selector: "app-integrations",
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: "./integrations.component.html",
  styleUrl: "./integrations.component.scss",
})
export class IntegrationsComponent implements OnInit {
  integrations = signal<Integration[]>([]);
  partners = signal<Partner[]>([]);
  loading = signal(true);
  showForm = signal(false);
  saving = signal(false);

  types: IntegrationType[] = [
    "REST_API",
    "GRAPHQL",
    "WEBHOOK",
    "FILE_SYNC",
    "EDI",
  ];
  statuses: IntegrationStatus[] = ["DRAFT", "CONNECTED", "FAILING", "PAUSED"];

  form = {
    partnerId: "",
    name: "",
    type: "REST_API" as IntegrationType,
    endpointUrl: "",
    scopesRaw: "",
  };

  constructor(
    private apollo: Apollo,
    private dialog: ConfirmService,
  ) {}

  ngOnInit(): void {
    this.refresh();
    this.apollo
      .watchQuery<{
        partners: Partner[];
      }>({ query: GET_PARTNERS, fetchPolicy: "network-only" })
      .valueChanges.subscribe((result) =>
        this.partners.set(result.data?.partners ?? []),
      );
  }

  refresh(): void {
    this.loading.set(true);
    this.apollo
      .watchQuery<{
        integrations: Integration[];
      }>({ query: GET_INTEGRATIONS, fetchPolicy: "network-only" })
      .valueChanges.subscribe((result) => {
        this.integrations.set(result.data?.integrations ?? []);
        this.loading.set(false);
      });
  }

  toggleForm(): void {
    this.showForm.update((v) => !v);
  }

  partnerName(partnerId: string): string {
    return (
      this.partners().find((p) => p.id === partnerId)?.companyName ??
      "Unknown partner"
    );
  }

  async createIntegration(): Promise<void> {
    this.saving.set(true);
    try {
      const scopes = this.form.scopesRaw
        .split(",")
        .map((s) => s.trim())
        .filter(Boolean);

      await firstValueFrom(
        this.apollo.mutate({
          mutation: CREATE_INTEGRATION,
          variables: {
            input: {
              partnerId: this.form.partnerId,
              name: this.form.name,
              type: this.form.type,
              endpointUrl: this.form.endpointUrl,
              scopes,
            },
          },
        }),
      );
      this.form = {
        partnerId: "",
        name: "",
        type: "REST_API",
        endpointUrl: "",
        scopesRaw: "",
      };
      this.showForm.set(false);
      this.refresh();
    } finally {
      this.saving.set(false);
    }
  }

  async changeStatus(
    integration: Integration,
    status: IntegrationStatus,
  ): Promise<void> {
    await firstValueFrom(
      this.apollo.mutate({
        mutation: UPDATE_INTEGRATION_STATUS,
        variables: { id: integration.id, status },
      }),
    );
    this.refresh();
  }

  async remove(integration: Integration): Promise<void> {
    const ok = await this.dialog.confirm({
      title: "Delete integration?",
      message: `Delete integration "${integration.name}"? This can't be undone.`,
      confirmText: "Delete",
      icon: "trash",
      tone: "danger",
    });
    if (!ok) return;
    await firstValueFrom(
      this.apollo.mutate({
        mutation: DELETE_INTEGRATION,
        variables: { id: integration.id },
      }),
    );
    this.refresh();
  }

  statusClass(status: IntegrationStatus): string {
    switch (status) {
      case "CONNECTED":
        return "live";
      case "DRAFT":
        return "idle";
      case "PAUSED":
        return "warn";
      default:
        return "danger";
    }
  }
}
