import { Component, signal } from "@angular/core";
import { CommonModule } from "@angular/common";
import { AuthService } from "../../../core/services/auth.service";
import { ConfirmService } from "../../../core/services/confirm.service";

@Component({
  selector: "app-navbar",
  standalone: true,
  imports: [CommonModule],
  templateUrl: "./navbar.component.html",
  styleUrl: "./navbar.component.scss",
})
export class NavbarComponent {
  signingOut = signal(false);

  constructor(
    public auth: AuthService,
    private dialog: ConfirmService,
  ) {}

  async signOut(): Promise<void> {
    if (this.signingOut()) return;

    const ok = await this.dialog.confirm({
      title: "Sign out?",
      message:
        "You will need to sign in again to get back into your control tower.",
      icon: "logout",
      tone: "primary",
    });
    if (!ok) return;

    this.signingOut.set(true);
    try {
      this.auth.logout();
    } finally {
      this.signingOut.set(false);
    }
  }
}
