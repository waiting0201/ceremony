import { inject } from '@angular/core';
import { type CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

export const authGuard: CanActivateFn = async () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (auth.isLoggedIn()) return true;

  // Try restoring session
  const hasSession = await auth.checkSession();
  if (hasSession) return true;

  return router.createUrlTree(['/login']);
};
