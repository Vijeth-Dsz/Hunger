import { Routes } from '@angular/router';
import { adminGuard, authenticatedGuard } from './core/services/auth.guard';
import { AuthPage } from './features/auth/auth-page';
import { DashboardShell } from './features/dashboard/dashboard-shell';

export const routes: Routes = [
  { path: '', redirectTo: 'login', pathMatch: 'full' },
  { path: 'login', component: AuthPage, title: 'Join Hunger' },
  { path: 'admin/login', component: AuthPage, data: { adminLogin: true }, title: 'Hunger admin sign in' },
  { path: 'dashboard', component: DashboardShell, canActivate: [authenticatedGuard], title: 'Hunger dashboard' },
  { path: 'admin', component: DashboardShell, canActivate: [adminGuard], title: 'Hunger administration' },
  { path: '**', redirectTo: 'login' },
];
