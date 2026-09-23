import { Component, OnDestroy, OnInit, signal } from "@angular/core";
import { CommonModule } from "@angular/common";
import { Apollo, QueryRef } from "apollo-angular";
import { Subscription } from "rxjs";
import {
  DASHBOARD_SUMMARY,
  GET_WEBHOOK_LOGS,
  ON_WEBHOOK_EVENT,
} from "../../core/graphql.operations";
import { DashboardSummary, WebhookLog } from "../../core/models/models";

@Component({
  selector: "app-dashboard",
  standalone: true,
  imports: [CommonModule],
  templateUrl: "./dashboard.component.html",
  styleUrl: "./dashboard.component.scss",
})
export class DashboardComponent implements OnInit, OnDestroy {
  summary = signal<DashboardSummary | null>(null);
  feed = signal<WebhookLog[]>([]);
  loading = signal(true);

  private summaryRef?: QueryRef<{ dashboardSummary: DashboardSummary }>;
  private subs = new Subscription();
  private refreshTimer?: ReturnType<typeof setTimeout>;

  constructor(private apollo: Apollo) {}

  ngOnInit(): void {
    // 1) Summary tiles (kept as a QueryRef so we can refetch it when new events arrive).
    this.summaryRef = this.apollo.watchQuery<{
      dashboardSummary: DashboardSummary;
    }>({
      query: DASHBOARD_SUMMARY,
      fetchPolicy: "network-only",
    });
    this.subs.add(
      this.summaryRef.valueChanges.subscribe({
        next: (result) => {
          this.summary.set(result.data?.dashboardSummary ?? null);
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      }),
    );

    // 2) Past events, so the activity list is filled as soon as the page opens.
    this.subs.add(
      this.apollo
        .query<{ webhookLogs: WebhookLog[] }>({
          query: GET_WEBHOOK_LOGS,
          variables: { limit: 20 },
          fetchPolicy: "network-only",
        })
        .subscribe({
          next: (result) => this.mergeFeed(result.data?.webhookLogs ?? []),
          error: () => {
            /* the live feed below still works even if history fails */
          },
        }),
    );

    // 3) Live events: every webhook the backend records streams in here in real time.
    this.subs.add(
      this.apollo
        .subscribe<{ onWebhookEvent: WebhookLog }>({ query: ON_WEBHOOK_EVENT })
        .subscribe({
          next: (result) => {
            const event = result.data?.onWebhookEvent;
            if (event) {
              this.mergeFeed([event]);
              this.scheduleSummaryRefresh();
            }
          },
          error: () => {
            /* connection dropped - the page keeps showing what it has */
          },
        }),
    );
  }

  ngOnDestroy(): void {
    this.subs.unsubscribe();
    clearTimeout(this.refreshTimer);
  }

  trackById(_index: number, event: WebhookLog): string {
    return event.id;
  }

  /** Adds events to the feed without duplicates, newest first, capped at 25. */
  private mergeFeed(incoming: WebhookLog[]): void {
    this.feed.update((current) => {
      const byId = new Map<string, WebhookLog>();
      [...incoming, ...current].forEach((e) => {
        if (!byId.has(e.id)) byId.set(e.id, e);
      });
      return Array.from(byId.values())
        .sort(
          (a, b) =>
            new Date(b.receivedAt).getTime() - new Date(a.receivedAt).getTime(),
        )
        .slice(0, 25);
    });
  }

  /**
   * Re-reads the summary numbers shortly after a new event. The small delay lets the
   * backend finish marking the integration as "Connected" before we ask for the counts.
   */
  private scheduleSummaryRefresh(): void {
    clearTimeout(this.refreshTimer);
    this.refreshTimer = setTimeout(() => {
      this.summaryRef?.refetch().catch(() => {
        /* ignore - next event will retry */
      });
    }, 600);
  }
}
