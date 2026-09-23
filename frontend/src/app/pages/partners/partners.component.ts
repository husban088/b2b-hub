import { Component, OnInit, signal } from "@angular/core";
import { CommonModule } from "@angular/common";
import { FormsModule } from "@angular/forms";
import { Apollo } from "apollo-angular";
import { firstValueFrom } from "rxjs";
import {
  GET_PARTNERS,
  CREATE_PARTNER,
  UPDATE_PARTNER,
  DELETE_PARTNER,
} from "../../core/graphql.operations";
import { Partner, PartnerStatus } from "../../core/models/models";
import { ConfirmService } from "../../core/services/confirm.service";

@Component({
  selector: "app-partners",
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: "./partners.component.html",
  styleUrl: "./partners.component.scss",
})
export class PartnersComponent implements OnInit {
  partners = signal<Partner[]>([]);
  loading = signal(true);
  showForm = signal(false);
  saving = signal(false);

  form = {
    companyName: "",
    companyEmail: "",
    country: "",
    industry: "",
  };

  statuses: PartnerStatus[] = ["PENDING", "ACTIVE", "SUSPENDED", "OFFBOARDED"];

  constructor(
    private apollo: Apollo,
    private dialog: ConfirmService,
  ) {}

  ngOnInit(): void {
    this.refresh();
  }

  refresh(): void {
    this.loading.set(true);
    this.apollo
      .watchQuery<{
        partners: Partner[];
      }>({ query: GET_PARTNERS, fetchPolicy: "network-only" })
      .valueChanges.subscribe((result) => {
        this.partners.set(result.data?.partners ?? []);
        this.loading.set(false);
      });
  }

  toggleForm(): void {
    this.showForm.update((v) => !v);
  }

  async createPartner(): Promise<void> {
    this.saving.set(true);
    try {
      await firstValueFrom(
        this.apollo.mutate({
          mutation: CREATE_PARTNER,
          variables: { input: { ...this.form } },
        }),
      );
      this.form = {
        companyName: "",
        companyEmail: "",
        country: "",
        industry: "",
      };
      this.showForm.set(false);
      this.refresh();
    } finally {
      this.saving.set(false);
    }
  }

  async changeStatus(partner: Partner, status: PartnerStatus): Promise<void> {
    await firstValueFrom(
      this.apollo.mutate({
        mutation: UPDATE_PARTNER,
        variables: {
          input: {
            id: partner.id,
            companyName: partner.companyName,
            country: partner.country,
            industry: partner.industry,
            status,
          },
        },
      }),
    );
    this.refresh();
  }

  async remove(partner: Partner): Promise<void> {
    const ok = await this.dialog.confirm({
      title: "Remove partner?",
      message: `Remove ${partner.companyName}? This can't be undone.`,
      confirmText: "Remove",
      icon: "trash",
      tone: "danger",
    });
    if (!ok) return;
    await firstValueFrom(
      this.apollo.mutate({
        mutation: DELETE_PARTNER,
        variables: { id: partner.id },
      }),
    );
    this.refresh();
  }

  statusClass(status: PartnerStatus): string {
    switch (status) {
      case "ACTIVE":
        return "live";
      case "PENDING":
        return "warn";
      case "SUSPENDED":
        return "danger";
      default:
        return "idle";
    }
  }
}
