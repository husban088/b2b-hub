import { Injectable, signal } from "@angular/core";

export type ConfirmTone = "primary" | "danger";
export type ConfirmIcon = "trash" | "plus" | "refresh" | "logout";

export interface ConfirmOptions {
  title: string;
  message: string;
  confirmText?: string;
  cancelText?: string;
  tone?: ConfirmTone;
  icon?: ConfirmIcon;
}

export interface ConfirmState {
  options: Required<ConfirmOptions>;
  resolve: (result: boolean) => void;
}

/**
 * Opens the luxury confirmation modal and resolves to true (OK) or false (Cancel / outside click / Esc).
 *
 *   const ok = await confirmService.confirm({ title: 'Delete?', message: '...', tone: 'danger', icon: 'trash' });
 *   if (!ok) return;
 */
@Injectable({ providedIn: "root" })
export class ConfirmService {
  readonly state = signal<ConfirmState | null>(null);

  confirm(options: ConfirmOptions): Promise<boolean> {
    // If another modal is somehow still open, treat it as cancelled.
    this.state()?.resolve(false);

    return new Promise<boolean>((resolve) => {
      this.state.set({
        options: {
          confirmText: "OK",
          cancelText: "Cancel",
          tone: "primary",
          icon: "plus",
          ...options,
        },
        resolve,
      });
    });
  }

  close(result: boolean): void {
    const current = this.state();
    if (!current) return;
    this.state.set(null);
    current.resolve(result);
  }
}
