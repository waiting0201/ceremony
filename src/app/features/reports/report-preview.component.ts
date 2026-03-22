import { Component, inject, signal, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatSelectModule } from '@angular/material/select';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { DomSanitizer, type SafeResourceUrl } from '@angular/platform-browser';
import { ReportsService } from './reports.service';

@Component({
  selector: 'app-report-preview',
  standalone: true,
  imports: [
    FormsModule, MatSelectModule, MatFormFieldModule,
    MatButtonModule, MatIconModule, MatProgressBarModule,
  ],
  template: `
    <div>
      <div class="mb-4 flex items-center justify-between">
        <h2 class="text-xl font-semibold text-gray-800">報表預覽</h2>
        <div class="flex gap-2">
          <mat-form-field appearance="outline" class="w-52">
            <mat-label>選擇報表</mat-label>
            <mat-select [(ngModel)]="selectedTemplate" (ngModelChange)="onTemplateChange()">
              @for (t of templates(); track t) {
                <mat-option [value]="t">{{ t }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
          <button mat-flat-button color="primary" (click)="onPrint()" [disabled]="!previewUrl()">
            <mat-icon>print</mat-icon> 列印
          </button>
          <button mat-stroked-button (click)="onExportPdf()" [disabled]="!previewUrl()">
            <mat-icon>picture_as_pdf</mat-icon> PDF
          </button>
        </div>
      </div>

      @if (loading()) {
        <mat-progress-bar mode="indeterminate" />
      }

      @if (previewUrl()) {
        <div class="rounded border border-gray-200 bg-white">
          <iframe [src]="previewUrl()" class="h-[calc(100vh-200px)] w-full" frameborder="0"></iframe>
        </div>
      } @else if (!loading()) {
        <div class="flex h-64 items-center justify-center text-gray-500">
          選擇報表範本並提供資料以預覽
        </div>
      }

      @if (message()) {
        <p class="mt-2 text-sm text-green-400">{{ message() }}</p>
      }
    </div>
  `,
})
export class ReportPreviewComponent implements OnInit {
  private reportsSvc = inject(ReportsService);
  private sanitizer = inject(DomSanitizer);

  templates = signal<string[]>([]);
  loading = signal(false);
  previewUrl = signal<SafeResourceUrl | null>(null);
  message = signal('');
  selectedTemplate = '';

  // This would be populated by the caller (e.g. from signup-list)
  private currentData: any[] = [];

  async ngOnInit(): Promise<void> {
    const r = await this.reportsSvc.listTemplates();
    if (r.success && r.data) this.templates.set(r.data);
  }

  setData(data: any[]): void {
    this.currentData = data;
  }

  async onTemplateChange(): Promise<void> {
    if (!this.selectedTemplate || this.currentData.length === 0) return;
    this.loading.set(true);
    const r = await this.reportsSvc.preview(this.selectedTemplate, this.currentData);
    if (r.success && r.data) {
      this.previewUrl.set(this.sanitizer.bypassSecurityTrustResourceUrl('file:///' + r.data.replace(/\\/g, '/')));
    }
    this.loading.set(false);
  }

  async onPrint(): Promise<void> {
    if (!this.selectedTemplate) return;
    await this.reportsSvc.print(this.selectedTemplate, this.currentData);
  }

  async onExportPdf(): Promise<void> {
    if (!this.selectedTemplate) return;
    const r = await this.reportsSvc.exportPdf(this.selectedTemplate, this.currentData);
    if (r.success && r.data) {
      this.message.set(`PDF 已儲存: ${r.data}`);
    }
  }
}
