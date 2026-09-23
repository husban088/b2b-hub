import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';

export const routes: Routes = [
  { path: 'login', loadComponent: () => import('./pages/login/login.component').then(m => m.LoginComponent) },
  {
    path: '',
    canActivate: [authGuard],
    children: [
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
      { path: 'dashboard', loadComponent: () => import('./pages/dashboard/dashboard.component').then(m => m.DashboardComponent) },
      { path: 'partners', loadComponent: () => import('./pages/partners/partners.component').then(m => m.PartnersComponent) },
      { path: 'integrations', loadComponent: () => import('./pages/integrations/integrations.component').then(m => m.IntegrationsComponent) }
    ]
  },
  { path: '**', redirectTo: 'dashboard' }
];
