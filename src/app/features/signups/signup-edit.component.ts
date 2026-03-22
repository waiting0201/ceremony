import { Component, inject, signal, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import {
  ZipcodeSelectComponent,
  type ZipcodeSelection,
} from '../../shared/components/zipcode-select/zipcode-select.component';
import {
  NameFieldGroupComponent,
  type NameFields,
} from '../../shared/components/name-field-group/name-field-group.component';
import { SignupsService } from './signups.service';
import { IpcService } from '../../core/services/ipc.service';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-signup-edit',
  standalone: true,
  imports: [
    FormsModule, MatFormFieldModule, MatInputModule, MatSelectModule,
    MatButtonModule, MatIconModule, MatProgressBarModule,
    ZipcodeSelectComponent, NameFieldGroupComponent,
  ],
  template: `
    <div class="mx-auto max-w-4xl">
      <div class="mb-4 flex items-center gap-3">
        <button mat-icon-button (click)="router.navigate(['/signups'])">
          <mat-icon class="text-gray-800">arrow_back</mat-icon>
        </button>
        <h2 class="text-xl font-semibold text-gray-800">編輯報名</h2>
      </div>

      @if (loading()) {
        <mat-progress-bar mode="indeterminate" />
      } @else if (form) {
        <div class="rounded-lg border border-gray-200 bg-white p-6 space-y-6">
          <div class="grid grid-cols-4 gap-4">
            <mat-form-field appearance="outline">
              <mat-label>年度</mat-label>
              <input matInput type="number" [(ngModel)]="form.Year" />
            </mat-form-field>
            <mat-form-field appearance="outline">
              <mat-label>號碼</mat-label>
              <input matInput type="number" [(ngModel)]="form.Number" />
            </mat-form-field>
            <mat-form-field appearance="outline">
              <mat-label>姓名</mat-label>
              <input matInput [(ngModel)]="form.Name" />
            </mat-form-field>
            <mat-form-field appearance="outline">
              <mat-label>費用</mat-label>
              <input matInput type="number" [(ngModel)]="form.Fee" />
            </mat-form-field>
          </div>

          <div class="grid grid-cols-3 gap-4">
            <mat-form-field appearance="outline">
              <mat-label>電話</mat-label>
              <input matInput [(ngModel)]="form.Phone" />
            </mat-form-field>
            <mat-form-field appearance="outline" class="col-span-2">
              <mat-label>備註</mat-label>
              <input matInput [(ngModel)]="form.Remark" />
            </mat-form-field>
          </div>

          <app-name-field-group label="往生" [initialValues]="deadNames()" (valuesChange)="onDeadChange($event)" />
          <app-name-field-group label="陽上" [initialValues]="livingNames()" (valuesChange)="onLivingChange($event)" />

          <div>
            <div class="mb-1 text-sm font-medium text-gray-800">郵寄地址</div>
            <app-zipcode-select
              [initialZipcodeId]="form.MailZipcodeID"
              [initialAddress]="form.MailAddress ?? ''"
              (selectionChange)="onMailChange($event)" />
          </div>
          <div>
            <div class="mb-1 text-sm font-medium text-gray-800">文疏地址</div>
            <app-zipcode-select
              [initialZipcodeId]="form.TextZipcodeID"
              [initialAddress]="form.TextAddress ?? ''"
              (selectionChange)="onTextChange($event)" />
          </div>

          @if (formError()) {
            <p class="text-sm text-red-400">{{ formError() }}</p>
          }

          <div class="flex gap-2 pt-2">
            <button mat-flat-button color="primary" (click)="onSave()" [disabled]="saving()">
              {{ saving() ? '儲存中...' : '儲存' }}
            </button>
            <button mat-button (click)="router.navigate(['/signups'])">取消</button>
          </div>
        </div>
      }
    </div>
  `,
})
export class SignupEditComponent implements OnInit {
  private svc = inject(SignupsService);
  private ipc = inject(IpcService);
  private auth = inject(AuthService);
  private route = inject(ActivatedRoute);
  router = inject(Router);

  loading = signal(true);
  saving = signal(false);
  formError = signal('');
  livingNames = signal<string[]>([]);
  deadNames = signal<string[]>([]);

  form: any = null;
  private signupId = '';

  async ngOnInit(): Promise<void> {
    this.signupId = this.route.snapshot.paramMap.get('id') ?? '';
    const r = await this.svc.get(this.signupId);
    if (r.success && r.data) {
      this.form = { ...r.data };
      const d = r.data;
      this.deadNames.set([d.DeadNameOne, d.DeadNameTwo, d.DeadNameThree, d.DeadNameFour, d.DeadNameFive, d.DeadNameSix].map((n: any) => n ?? ''));
      this.livingNames.set([d.LivingNameOne, d.LivingNameTwo, d.LivingNameThree, d.LivingNameFour, d.LivingNameFive, d.LivingNameSix].map((n: any) => n ?? ''));
    }
    this.loading.set(false);
  }

  onDeadChange(n: NameFields): void {
    this.form.DeadNameOne = n.one || null; this.form.DeadNameTwo = n.two || null;
    this.form.DeadNameThree = n.three || null; this.form.DeadNameFour = n.four || null;
    this.form.DeadNameFive = n.five || null; this.form.DeadNameSix = n.six || null;
  }

  onLivingChange(n: NameFields): void {
    this.form.LivingNameOne = n.one || null; this.form.LivingNameTwo = n.two || null;
    this.form.LivingNameThree = n.three || null; this.form.LivingNameFour = n.four || null;
    this.form.LivingNameFive = n.five || null; this.form.LivingNameSix = n.six || null;
  }

  onMailChange(s: ZipcodeSelection): void {
    this.form.MailZipcodeID = s.zipcodeId; this.form.MailZipcode = s.zipcode; this.form.MailAddress = s.address;
  }

  onTextChange(s: ZipcodeSelection): void {
    this.form.TextZipcodeID = s.zipcodeId; this.form.TextZipcode = s.zipcode; this.form.TextAddress = s.address;
  }

  async onSave(): Promise<void> {
    this.saving.set(true);
    this.formError.set('');

    // Create audit log before saving
    const session = this.auth.session();
    await this.ipc.invoke('signup-logs:create', {
      SignupLogID: crypto.randomUUID(),
      SignupID: this.signupId,
      Year: this.form.Year,
      CeremonyCategoryTitle: this.form.CeremonyTitle || '',
      SignupType: this.form.SignupType,
      HallName: this.form.HallName,
      Name: this.form.Name,
      Phone: this.form.Phone,
      NumberTitle: this.form.NumberTitle,
      Number: this.form.Number,
      Fee: this.form.Fee,
      LivingNameOne: this.form.LivingNameOne, LivingNameTwo: this.form.LivingNameTwo,
      LivingNameThree: this.form.LivingNameThree, LivingNameFour: this.form.LivingNameFour,
      LivingNameFive: this.form.LivingNameFive, LivingNameSix: this.form.LivingNameSix,
      DeadNameOne: this.form.DeadNameOne, DeadNameTwo: this.form.DeadNameTwo,
      DeadNameThree: this.form.DeadNameThree, DeadNameFour: this.form.DeadNameFour,
      DeadNameFive: this.form.DeadNameFive, DeadNameSix: this.form.DeadNameSix,
      MailCity: '', MailZone: '', MailAddress: this.form.MailAddress,
      TextCity: '', TextZone: '', TextAddress: this.form.TextAddress,
      Remark: this.form.Remark,
      Admin: session?.Name || 'unknown',
      Createdate: new Date().toISOString(),
    });

    const result = await this.svc.update(this.signupId, this.form);
    this.saving.set(false);

    if (result.success) {
      this.router.navigate(['/signups']);
    } else {
      this.formError.set(result.message || '儲存失敗');
    }
  }
}
