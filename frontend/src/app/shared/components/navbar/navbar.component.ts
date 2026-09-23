import { Component } from "@angular/core";
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
  constructor(
    public auth: AuthService,
    private dialog: ConfirmService,
  ) {}

  async signOut(): Promise<void> {
    const ok = await this.dialog.confirm({
      title: "Sign out?",
      message:
        "You will need to sign in again to get back into your control tower.",
      icon: "logout",
      tone: "primary",
    });
    if (ok) this.auth.logout();
  }
}
