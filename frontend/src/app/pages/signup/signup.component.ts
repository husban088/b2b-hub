import { Component, signal } from "@angular/core";
import { CommonModule } from "@angular/common";
import { FormsModule } from "@angular/forms";
import { Router, RouterLink } from "@angular/router";
import { ApolloError } from "@apollo/client/core";
import { AuthService } from "../../core/services/auth.service";

@Component({
  selector: "app-signup",
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: "./signup.component.html",
  styleUrl: "./signup.component.scss",
})
export class SignupComponent {
  fullName = "";
  email = "";
  password = "";
  confirmPassword = "";
  companyName = "";
  vatNumber = "";
  iban = "";

  loading = signal(false);
  showPassword = signal(false);

  fullNameError = signal<string | null>(null);
  emailError = signal<string | null>(null);
  passwordError = signal<string | null>(null);
  confirmPasswordError = signal<string | null>(null);
  formError = signal<string | null>(null);

  constructor(
    private auth: AuthService,
    private router: Router,
  ) {}

  togglePassword(): void {
    this.showPassword.update((v) => !v);
  }

  onFullNameChange(value: string): void {
    this.fullName = value;
    this.fullNameError.set(null);
    this.formError.set(null);
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

  onConfirmPasswordChange(value: string): void {
    this.confirmPassword = value;
    this.confirmPasswordError.set(null);
    this.formError.set(null);
  }

  async submit(): Promise<void> {
    this.fullNameError.set(null);
    this.emailError.set(null);
    this.passwordError.set(null);
    this.confirmPasswordError.set(null);
    this.formError.set(null);

    if (this.password.length < 8) {
      this.passwordError.set("Password must be at least 8 characters.");
      return;
    }
    if (this.password !== this.confirmPassword) {
      this.confirmPasswordError.set("Passwords do not match.");
      return;
    }

    this.loading.set(true);
    try {
      await this.auth.register(
        this.fullName.trim(),
        this.email.trim(),
        this.password,
        this.companyName.trim(),
        this.vatNumber.trim(),
        this.iban.trim(),
      );
      this.router.navigate(["/dashboard"]);
    } catch (err) {
      this.showSignupError(err);
    } finally {
      this.loading.set(false);
    }
  }

  private showSignupError(err: unknown): void {
    const message = this.graphQLMessage(err);

    if (message && message.toLowerCase().includes("email")) {
      // e.g. "A user with this email already exists."
      this.emailError.set(message);
    } else if (message) {
      this.formError.set(message);
    } else {
      this.formError.set(
        "We could not create your account right now. Please check your connection and try again.",
      );
    }
  }

  private graphQLMessage(err: unknown): string | null {
    const first = (err as ApolloError)?.graphQLErrors?.[0];
    return first?.message ?? null;
  }
}
