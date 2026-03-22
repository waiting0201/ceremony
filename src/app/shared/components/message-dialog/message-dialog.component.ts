import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';

export interface MessageDialogData {
  title: string;
  message: string;
  type?: 'info' | 'success' | 'error' | 'warning';
}

@Component({
  selector: 'app-message-dialog',
  standalone: true,
  imports: [MatDialogModule, MatButtonModule, MatIconModule],
  template: `
    <h2 mat-dialog-title class="flex items-center gap-2 !text-gray-800">
      @switch (data.type) {
        @case ('success') { <mat-icon class="text-green-600">check_circle</mat-icon> }
        @case ('error') { <mat-icon class="text-red-500">error</mat-icon> }
        @case ('warning') { <mat-icon class="text-amber-500">warning</mat-icon> }
        @default { <mat-icon class="text-indigo-600">info</mat-icon> }
      }
      {{ data.title }}
    </h2>
    <mat-dialog-content class="!text-gray-800">
      <p>{{ data.message }}</p>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-flat-button color="primary" (click)="dialogRef.close()">確定</button>
    </mat-dialog-actions>
  `,
})
export class MessageDialogComponent {
  data = inject<MessageDialogData>(MAT_DIALOG_DATA);
  dialogRef = inject(MatDialogRef<MessageDialogComponent>);
}
