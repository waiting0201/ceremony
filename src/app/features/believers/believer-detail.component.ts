import { Component, inject, signal, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatTabsModule } from '@angular/material/tabs';
import {
  ZipcodeSelectComponent,
  type ZipcodeSelection,
} from '../../shared/components/zipcode-select/zipcode-select.component';
import {
  NameFieldGroupComponent,
  type NameFields,
} from '../../shared/components/name-field-group/name-field-group.component';
import { BelieversService, type Believer } from './believers.service';

const EMPLOYEE_TYPES = [
  { value: 0, label: '一般' },
  { value: 1, label: '志工' },
  { value: 2, label: '員工' },
  { value: 3, label: '委員' },
  { value: 4, label: '執事' },
  { value: 5, label: '常住' },
];

@Component({
  selector: 'app-believer-detail',
  standalone: true,
  imports: [
    FormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatSlideToggleModule,
    MatTabsModule,
    ZipcodeSelectComponent,
    NameFieldGroupComponent,
  ],
  template: `
    <h2 mat-dialog-title class="!text-gray-800">
      {{ data ? '編輯信眾' : '新增信眾' }}
    </h2>
    <mat-dialog-content class="!max-h-[70vh]">
      <mat-tab-group class="dark-tabs">
        <!-- 基本資料 -->
        <mat-tab label="基本資料">
          <div class="grid grid-cols-3 gap-4 pt-4">
            <mat-form-field appearance="outline">
              <mat-label>姓名</mat-label>
              <input matInput [(ngModel)]="form.Name" />
            </mat-form-field>
            <mat-form-field appearance="outline">
              <mat-label>堂名</mat-label>
              <input matInput [(ngModel)]="form.HallName" />
            </mat-form-field>
            <mat-form-field appearance="outline">
              <mat-label>電話</mat-label>
              <input matInput [(ngModel)]="form.Phone" />
            </mat-form-field>
            <mat-form-field appearance="outline">
              <mat-label>身份</mat-label>
              <mat-select [(ngModel)]="form.EmployeeType">
                @for (t of employeeTypes; track t.value) {
                  <mat-option [value]="t.value">{{ t.label }}</mat-option>
                }
              </mat-select>
            </mat-form-field>
            <div class="flex items-center">
              <mat-slide-toggle [(ngModel)]="form.IsFixedNumber">固定號碼</mat-slide-toggle>
            </div>
          </div>
        </mat-tab>

        <!-- 通訊地址 -->
        <mat-tab label="通訊地址">
          <div class="pt-4 space-y-4">
            <div>
              <div class="mb-1 text-sm font-medium text-gray-800">郵寄地址</div>
              <app-zipcode-select
                [initialZipcodeId]="form.MailZipcodeID ?? null"
                [initialAddress]="form.MailAddress ?? ''"
                (selectionChange)="onMailZipcodeChange($event)" />
            </div>
            <div>
              <div class="mb-1 text-sm font-medium text-gray-800">文疏地址</div>
              <app-zipcode-select
                [initialZipcodeId]="form.TextZipcodeID ?? null"
                [initialAddress]="form.TextAddress ?? ''"
                (selectionChange)="onTextZipcodeChange($event)" />
            </div>
          </div>
        </mat-tab>

        <!-- 陽上/往生 -->
        <mat-tab label="陽上/往生">
          <div class="pt-4 space-y-4">
            <app-name-field-group
              label="往生"
              [initialValues]="deadNames()"
              (valuesChange)="onDeadNamesChange($event)" />
            <app-name-field-group
              label="陽上"
              [initialValues]="livingNames()"
              (valuesChange)="onLivingNamesChange($event)" />
          </div>
        </mat-tab>
      </mat-tab-group>

      @if (formError()) {
        <p class="mt-2 text-sm text-red-400">{{ formError() }}</p>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button (click)="dialogRef.close(false)">取消</button>
      <button mat-flat-button color="primary" (click)="onSave()" [disabled]="saving()">
        {{ saving() ? '儲存中...' : '儲存' }}
      </button>
    </mat-dialog-actions>
  `,
})
export class BelieverDetailComponent implements OnInit {
  private svc = inject(BelieversService);
  data = inject<Believer | null>(MAT_DIALOG_DATA);
  dialogRef = inject(MatDialogRef<BelieverDetailComponent>);

  employeeTypes = EMPLOYEE_TYPES;
  formError = signal('');
  saving = signal(false);

  form: Partial<Believer> = {};

  livingNames = signal<string[]>([]);
  deadNames = signal<string[]>([]);

  ngOnInit(): void {
    if (this.data) {
      this.form = { ...this.data };
      this.livingNames.set([
        this.data.LivingNameOne ?? '',
        this.data.LivingNameTwo ?? '',
        this.data.LivingNameThree ?? '',
        this.data.LivingNameFour ?? '',
        this.data.LivingNameFive ?? '',
        this.data.LivingNameSix ?? '',
      ]);
      this.deadNames.set([
        this.data.DeadNameOne ?? '',
        this.data.DeadNameTwo ?? '',
        this.data.DeadNameThree ?? '',
        this.data.DeadNameFour ?? '',
        this.data.DeadNameFive ?? '',
        this.data.DeadNameSix ?? '',
      ]);
    } else {
      this.form = { EmployeeType: 0, IsFixedNumber: false };
    }
  }

  onMailZipcodeChange(sel: ZipcodeSelection): void {
    this.form.MailZipcodeID = sel.zipcodeId;
    this.form.MailZipcode = sel.zipcode;
    this.form.MailAddress = sel.address;
  }

  onTextZipcodeChange(sel: ZipcodeSelection): void {
    this.form.TextZipcodeID = sel.zipcodeId;
    this.form.TextZipcode = sel.zipcode;
    this.form.TextAddress = sel.address;
  }

  onLivingNamesChange(names: NameFields): void {
    this.form.LivingNameOne = names.one || null;
    this.form.LivingNameTwo = names.two || null;
    this.form.LivingNameThree = names.three || null;
    this.form.LivingNameFour = names.four || null;
    this.form.LivingNameFive = names.five || null;
    this.form.LivingNameSix = names.six || null;
  }

  onDeadNamesChange(names: NameFields): void {
    this.form.DeadNameOne = names.one || null;
    this.form.DeadNameTwo = names.two || null;
    this.form.DeadNameThree = names.three || null;
    this.form.DeadNameFour = names.four || null;
    this.form.DeadNameFive = names.five || null;
    this.form.DeadNameSix = names.six || null;
  }

  async onSave(): Promise<void> {
    if (!this.form.Name?.trim()) {
      this.formError.set('姓名為必填');
      return;
    }

    this.saving.set(true);
    this.formError.set('');

    const result = this.data
      ? await this.svc.update(this.data.BelieverID, this.form)
      : await this.svc.create({ ...this.form, BelieverID: crypto.randomUUID() });

    this.saving.set(false);

    if (result.success) {
      this.dialogRef.close(true);
    } else {
      this.formError.set(result.message || '儲存失敗');
    }
  }
}
