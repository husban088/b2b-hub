import {
  Component,
  ElementRef,
  HostListener,
  ViewChild,
  effect,
  signal,
} from "@angular/core";
import { CommonModule } from "@angular/common";
import { ConfirmService } from "../../../core/services/confirm.service";

@Component({
  selector: "app-confirm-modal",
  standalone: true,
  imports: [CommonModule],
  templateUrl: "./confirm-modal.component.html",
  styleUrl: "./confirm-modal.component.scss",
})
export class ConfirmModalComponent {
  closing = signal(false);

  @ViewChild("cancelBtn") cancelBtn?: ElementRef<HTMLButtonElement>;
  @ViewChild("okBtn") okBtn?: ElementRef<HTMLButtonElement>;

  /** True only when the press started on the dark backdrop (so text-selection drags don't close the modal). */
  private pressStartedOnBackdrop = false;

  constructor(public confirm: ConfirmService) {
    effect(() => {
      const open = !!this.confirm.state();
      // Lock page scroll behind the modal.
      document.body.style.overflow = open ? "hidden" : "";
      if (open) {
        setTimeout(() => this.focusDefault(), 60);
      }
    });
  }

  @HostListener("document:keydown.escape")
  onEscape(): void {
    if (this.confirm.state()) this.dismiss(false);
  }

  onBackdropPress(event: Event): void {
    this.pressStartedOnBackdrop = event.target === event.currentTarget;
  }

  /** Click on the dark area outside the card = Cancel. Clicks inside the card never reach here. */
  onBackdropClick(event: Event): void {
    if (this.pressStartedOnBackdrop && event.target === event.currentTarget) {
      this.dismiss(false);
    }
    this.pressStartedOnBackdrop = false;
  }

  onKeydown(event: KeyboardEvent): void {
    if (event.key !== "Tab") return;
    const first = this.cancelBtn?.nativeElement;
    const last = this.okBtn?.nativeElement;
    if (!first || !last) return;

    if (event.shiftKey && document.activeElement === first) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && document.activeElement === last) {
      event.preventDefault();
      first.focus();
    }
  }

  dismiss(result: boolean): void {
    if (this.closing()) return;
    this.closing.set(true);
    setTimeout(() => {
      this.confirm.close(result);
      this.closing.set(false);
    }, 170);
  }

  private focusDefault(): void {
    const tone = this.confirm.state()?.options.tone;
    // For destructive actions the safe choice (Cancel) is focused first.
    (tone === "danger" ? this.cancelBtn : this.okBtn)?.nativeElement.focus();
  }
}
