import { Routes } from '@angular/router';
import { authenticatedGuard } from './core/services/auth.guard';
import { AuthPage } from './features/auth/auth-page';
import { DashboardPage } from './features/dashboard/dashboard-page';

export const routes: Routes = [
  { path: '', redirectTo: 'login', pathMatch: 'full' },
  { path: 'login', component: AuthPage, title: 'Join Hunger' },
  { path: 'dashboard', component: DashboardPage, canActivate: [authenticatedGuard], title: 'Hunger dashboard' },
  { path: 'admin', component: DashboardPage, canActivate: [authenticatedGuard], title: 'Hunger administration' },
  { path: '**', redirectTo: 'login' },
];
