import { Component, signal } from "@angular/core";
import { CommonModule } from "@angular/common";
import { FormsModule } from "@angular/forms";
import { Router } from "@angular/router";
import { ApolloError } from "@apollo/client/core";
import { AuthService } from "../../core/services/auth.service";

@Component({
  selector: "app-login",
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: "./login.component.html",
  styleUrl: "./login.component.scss",
})
export class LoginComponent {
  email = "";
  password = "";

  loading = signal(false);
  showPassword = signal(false);

  /** Red message shown right under the email input. */
  emailError = signal<string | null>(null);
  /** Red message shown right under the password input. */
  passwordError = signal<string | null>(null);
  /** Fallback message for problems that are not about a specific input (e.g. server unreachable). */
  formError = signal<string | null>(null);

  constructor(
    private auth: AuthService,
    private router: Router,
  ) {}

  togglePassword(): void {
    this.showPassword.update((v) => !v);
  }

  onEmailChange(value: string): void {
    this.email = value;
    this.emailError.set(null);
    this.formError.set(null);
  }

  onPasswordChange(value: string): void {
    this.password = value;
    this.passwordError.set(null);
    this.formError.set(null);
  }

  async submit(): Promise<void> {
    this.emailError.set(null);
    this.passwordError.set(null);
    this.formError.set(null);
    this.loading.set(true);

    try {
      await this.auth.login(this.email.trim(), this.password);
      this.router.navigate(["/dashboard"]);
    } catch (err) {
      this.showLoginError(err);
    } finally {
      this.loading.set(false);
    }
  }

  private showLoginError(err: unknown): void {
    const code = this.errorCode(err);

    if (code === "EMAIL_NOT_FOUND") {
      // No account with this email, so the password can't be checked either: flag both inputs.
      this.emailError.set("No account found with this email address.");
      this.passwordError.set(
        "Password could not be verified. Please check it as well.",
      );
    } else if (code === "WRONG_PASSWORD") {
      this.passwordError.set("Incorrect password. Please try again.");
    } else {
      this.formError.set(
        "We could not sign you in right now. Please check your connection and try again.",
      );
    }
  }

  private errorCode(err: unknown): string | null {
    const first = (err as ApolloError)?.graphQLErrors?.[0];
    const code = first?.extensions?.["code"];
    return typeof code === "string" ? code : null;
  }
}
