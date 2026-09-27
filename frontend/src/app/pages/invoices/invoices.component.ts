import { Component, OnDestroy, OnInit, signal } from "@angular/core";
import { CommonModule } from "@angular/common";
import { FormsModule } from "@angular/forms";
import { Apollo } from "apollo-angular";
import { Subscription as RxSubscription, firstValueFrom } from "rxjs";
import {
  GET_INVOICES,
  CREATE_INVOICE,
  DELETE_INVOICE,
  ON_INVOICE_PAID,
  GET_PARTNERS,
} from "../../core/graphql.operations";
import { Invoice, InvoiceStatus, Partner } from "../../core/models/models";
import { environment } from "../../../environments/environment";
import { readAuthToken } from "../../core/graphql.provider";
import { ConfirmService } from "../../core/services/confirm.service";

@Component({
  selector: "app-invoices",
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: "./invoices.component.html",
  styleUrl: "./invoices.component.scss",
})
export class InvoicesComponent implements OnInit, OnDestroy {
  invoices = signal<Invoice[]>([]);
  partners = signal<Partner[]>([]);
  loading = signal(true);
  showForm = signal(false);
  saving = signal(false);
  formError = signal<string | null>(null);

  /** Invoice id currently being deleted / downloaded — lets each row show its own spinner
   * and stops a second click on the same row while the first request is still in flight. */
  deletingId = signal<string | null>(null);
  downloadingId = signal<string | null>(null);

  form = {
    partnerId: "",
    description: "",
    amount: null as number | null,
  };

  private paidSub?: RxSubscription;

  constructor(
    private apollo: Apollo,
    private dialog: ConfirmService,
  ) {}

  ngOnInit(): void {
    this.refresh();
    this.loadPartners();
    this.listenForPayments();
  }

  ngOnDestroy(): void {
    this.paidSub?.unsubscribe();
  }

  refresh(): void {
    this.loading.set(true);
    this.apollo
      .watchQuery<{
        invoices: Invoice[];
      }>({ query: GET_INVOICES, fetchPolicy: "network-only" })
      .valueChanges.subscribe((result) => {
        this.invoices.set(result.data?.invoices ?? []);
        this.loading.set(false);
      });
  }

  loadPartners(): void {
    this.apollo
      .watchQuery<{
        partners: Partner[];
      }>({ query: GET_PARTNERS, fetchPolicy: "network-only" })
      .valueChanges.subscribe((result) => {
        this.partners.set(result.data?.partners ?? []);
      });
  }

  /** Live "PAID" update - the moment Stripe confirms payment, the badge flips without a refresh. */
  private listenForPayments(): void {
    this.paidSub = this.apollo
      .subscribe<{ onInvoicePaid: Invoice }>({ query: ON_INVOICE_PAID })
      .subscribe((result) => {
        const paid = result.data?.onInvoicePaid;
        if (!paid) return;
        this.invoices.update((list) =>
          list.map((inv) => (inv.id === paid.id ? paid : inv)),
        );
      });
  }

  toggleForm(): void {
    this.showForm.update((v) => !v);
    this.formError.set(null);
  }

  clientName(partnerId: string): string {
    return this.partners().find((p) => p.id === partnerId)?.companyName ?? "—";
  }

  vatPreview(): number {
    return Math.round((this.form.amount ?? 0) * 0.19 * 100) / 100;
  }

  totalPreview(): number {
    return (
      Math.round(((this.form.amount ?? 0) + this.vatPreview()) * 100) / 100
    );
  }

  async createInvoice(): Promise<void> {
    if (
      !this.form.partnerId ||
      !this.form.description.trim() ||
      !this.form.amount
    ) {
      this.formError.set("Please fill in client, description and amount.");
      return;
    }

    this.saving.set(true);
    this.formError.set(null);
    try {
      await firstValueFrom(
        this.apollo.mutate({
          mutation: CREATE_INVOICE,
          variables: {
            input: {
              partnerId: this.form.partnerId,
              description: this.form.description.trim(),
              amount: this.form.amount,
            },
          },
        }),
      );
      this.form = { partnerId: "", description: "", amount: null };
      this.showForm.set(false);
      this.refresh();
    } catch (err) {
      this.formError.set(
        "Could not create the invoice. The Stripe payment link may have failed - check the backend logs.",
      );
    } finally {
      this.saving.set(false);
    }
  }

  async remove(invoice: Invoice): Promise<void> {
    if (this.deletingId()) return;

    const ok = await this.dialog.confirm({
      title: "Delete invoice?",
      message: `Delete invoice ${invoice.invoiceNumber}? This can't be undone.`,
      confirmText: "Delete",
      icon: "trash",
      tone: "danger",
    });
    if (!ok) return;

    this.deletingId.set(invoice.id);
    try {
      await firstValueFrom(
        this.apollo.mutate({
          mutation: DELETE_INVOICE,
          variables: { id: invoice.id },
        }),
      );
      this.refresh();
    } catch (err) {
      this.formError.set("Could not delete the invoice. Please try again.");
    } finally {
      this.deletingId.set(null);
    }
  }

  /** Downloads the invoice PDF. A plain <a href> can't send the JWT as a header, so this
   * fetches the file with the Authorization header attached and saves it via a blob URL. */
  async downloadPdf(invoice: Invoice): Promise<void> {
    if (this.downloadingId()) return;

    this.downloadingId.set(invoice.id);
    this.formError.set(null);
    try {
      const token = readAuthToken();
      const response = await fetch(
        `${environment.apiBaseUrl}/api/invoices/${invoice.id}/pdf`,
        {
          headers: token ? { Authorization: `Bearer ${token}` } : {},
        },
      );
      if (!response.ok) {
        this.formError.set("Could not download the PDF. Please try again.");
        return;
      }

      const blob = await response.blob();
      const blobUrl = window.URL.createObjectURL(blob);
      const link = document.createElement("a");
      link.href = blobUrl;
      link.download = `${invoice.invoiceNumber}.pdf`;
      link.click();
      window.URL.revokeObjectURL(blobUrl);
    } catch (err) {
      this.formError.set("Could not download the PDF. Please try again.");
    } finally {
      this.downloadingId.set(null);
    }
  }

  statusClass(status: InvoiceStatus): string {
    switch (status) {
      case "PAID":
        return "live";
      case "CANCELLED":
        return "danger";
      default:
        return "warn";
    }
  }
}
