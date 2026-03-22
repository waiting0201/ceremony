import { ErrorHandler, Injectable, inject } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';

@Injectable()
export class GlobalErrorHandler implements ErrorHandler {
  private snackBar = inject(MatSnackBar);

  handleError(error: any): void {
    const message = error?.message || error?.toString() || '發生未知錯誤';
    console.error('[GlobalError]', error);

    // Show user-friendly snackbar
    this.snackBar.open(message, '關閉', {
      duration: 5000,
      panelClass: ['error-snackbar'],
      horizontalPosition: 'center',
      verticalPosition: 'bottom',
    });
  }
}
