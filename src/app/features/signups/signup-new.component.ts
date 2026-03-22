import { Component, inject, signal, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
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

interface CategoryOption { CeremonyCategoryID: string; Title: string; }
interface BelieverOption { BelieverID: string; Name: string; Phone: string | null; HallName: string | null; }

@Component({
  selector: 'app-signup-new',
  standalone: true,
  imports: [
    FormsModule, MatFormFieldModule, MatInputModule, MatSelectModule,
    MatButtonModule, MatIconModule, MatAutocompleteModule, MatProgressBarModule,
    ZipcodeSelectComponent, NameFieldGroupComponent,
  ],
  template: `
    <div class="mx-auto max-w-4xl">
      <div class="mb-4 flex items-center gap-3">
        <button mat-icon-button (click)="router.navigate(['/signups'])">
          <mat-icon class="text-gray-800">arrow_back</mat-icon>
        </button>
        <h2 class="text-xl font-semibold text-gray-800">新增報名</h2>
      </div>

      <div class="rounded-lg border border-gray-200 bg-white p-6 space-y-6">
        <!-- Row 1: 基本 -->
        <div class="grid grid-cols-4 gap-4">
          <mat-form-field appearance="outline">
            <mat-label>年度</mat-label>
            <input matInput type="number" [(ngModel)]="form.Year" (ngModelChange)="onContextChange()" />
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>法會類別</mat-label>
            <mat-select [(ngModel)]="form.CeremonyCategoryID" (ngModelChange)="onContextChange()">
              @for (c of categories(); track c.CeremonyCategoryID) {
                <mat-option [value]="c.CeremonyCategoryID">{{ c.Title }}</mat-option>
              }
            </mat-select>
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>報名類型</mat-label>
            <mat-select [(ngModel)]="form.SignupType" (ngModelChange)="onContextChange()">
              <mat-option [value]="1">一般</mat-option>
              <mat-option [value]="2">預繳</mat-option>
              <mat-option [value]="3">特殊</mat-option>
            </mat-select>
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>號碼</mat-label>
            <input matInput type="number" [(ngModel)]="form.Number" />
          </mat-form-field>
        </div>

        <!-- Row 2: 信眾查詢 -->
        <div class="grid grid-cols-4 gap-4">
          <mat-form-field appearance="outline" class="col-span-2">
            <mat-label>信眾查詢 (輸入姓名/電話)</mat-label>
            <input matInput [(ngModel)]="believerSearch"
                   [matAutocomplete]="auto" (ngModelChange)="onBelieverSearch($event)" />
            <mat-autocomplete #auto="matAutocomplete" (optionSelected)="onBelieverSelect($event.option.value)">
              @for (b of believerOptions(); track b.BelieverID) {
                <mat-option [value]="b.BelieverID">
                  {{ b.Name }} {{ b.HallName ? '(' + b.HallName + ')' : '' }} {{ b.Phone }}
                </mat-option>
              }
            </mat-autocomplete>
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

        <!-- Row 3: 電話/備註 -->
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

        <!-- 往生/陽上 -->
        <app-name-field-group label="往生" [initialValues]="deadNames()" (valuesChange)="onDeadChange($event)" />
        <app-name-field-group label="陽上" [initialValues]="livingNames()" (valuesChange)="onLivingChange($event)" />

        <!-- 郵寄地址 -->
        <div>
          <div class="mb-1 text-sm font-medium text-gray-800">郵寄地址</div>
          <app-zipcode-select
            [initialZipcodeId]="form.MailZipcodeID ?? null"
            [initialAddress]="form.MailAddress ?? ''"
            (selectionChange)="onMailChange($event)" />
        </div>

        <!-- 文疏地址 -->
        <div>
          <div class="mb-1 text-sm font-medium text-gray-800">文疏地址</div>
          <app-zipcode-select
            [initialZipcodeId]="form.TextZipcodeID ?? null"
            [initialAddress]="form.TextAddress ?? ''"
            (selectionChange)="onTextChange($event)" />
        </div>

        @if (formError()) {
          <p class="text-sm text-red-400">{{ formError() }}</p>
        }

        <div class="flex gap-2 pt-2">
          <button mat-flat-button color="primary" (click)="onSave()" [disabled]="saving()">
            {{ saving() ? '儲存中...' : '儲存報名' }}
          </button>
          <button mat-button (click)="router.navigate(['/signups'])">取消</button>
        </div>
      </div>
    </div>
  `,
})
export class SignupNewComponent implements OnInit {
  private svc = inject(SignupsService);
  private ipc = inject(IpcService);
  private auth = inject(AuthService);
  router = inject(Router);

  categories = signal<CategoryOption[]>([]);
  believerOptions = signal<BelieverOption[]>([]);
  livingNames = signal<string[]>([]);
  deadNames = signal<string[]>([]);
  formError = signal('');
  saving = signal(false);
  believerSearch = '';

  form: any = {
    Year: new Date().getFullYear() - 1911,
    SignupType: 1,
    Number: null,
    Fee: null,
  };

  private searchTimer: any;

  async ngOnInit(): Promise<void> {
    const catResult = await this.ipc.invoke<CategoryOption[]>('ceremony-categories:list');
    if (catResult.success && catResult.data) this.categories.set(catResult.data);
  }

  async onContextChange(): Promise<void> {
    if (this.form['Year'] && this.form['CeremonyCategoryID'] && this.form['SignupType']) {
      const r = await this.svc.getNextNumber(
        this.form['Year'], this.form['CeremonyCategoryID'], this.form['SignupType'],
      );
      if (r.success && r.data) this.form['Number'] = r.data;
    }
  }

  onBelieverSearch(keyword: string): void {
    clearTimeout(this.searchTimer);
    if (keyword.length < 1) return;
    this.searchTimer = setTimeout(async () => {
      const r = await this.ipc.invoke<BelieverOption[]>('believers:lookup', keyword);
      if (r.success && r.data) this.believerOptions.set(r.data);
    }, 300);
  }

  async onBelieverSelect(believerId: string): Promise<void> {
    const r = await this.ipc.invoke<any>('believers:get', believerId);
    if (r.success && r.data) {
      const b = r.data;
      this.form['BelieverID'] = b.BelieverID;
      this.form['Name'] = b.Name;
      this.form['Phone'] = b.Phone;
      this.form['MailZipcodeID'] = b.MailZipcodeID;
      this.form['MailZipcode'] = b.MailZipcode;
      this.form['MailAddress'] = b.MailAddress;
      this.form['TextZipcodeID'] = b.TextZipcodeID;
      this.form['TextZipcode'] = b.TextZipcode;
      this.form['TextAddress'] = b.TextAddress;
      this.deadNames.set([b.DeadNameOne, b.DeadNameTwo, b.DeadNameThree, b.DeadNameFour, b.DeadNameFive, b.DeadNameSix].map((n: any) => n ?? ''));
      this.livingNames.set([b.LivingNameOne, b.LivingNameTwo, b.LivingNameThree, b.LivingNameFour, b.LivingNameFive, b.LivingNameSix].map((n: any) => n ?? ''));
      this.believerSearch = b.Name;
    }
  }

  onDeadChange(names: NameFields): void {
    this.form['DeadNameOne'] = names.one || null;
    this.form['DeadNameTwo'] = names.two || null;
    this.form['DeadNameThree'] = names.three || null;
    this.form['DeadNameFour'] = names.four || null;
    this.form['DeadNameFive'] = names.five || null;
    this.form['DeadNameSix'] = names.six || null;
  }

  onLivingChange(names: NameFields): void {
    this.form['LivingNameOne'] = names.one || null;
    this.form['LivingNameTwo'] = names.two || null;
    this.form['LivingNameThree'] = names.three || null;
    this.form['LivingNameFour'] = names.four || null;
    this.form['LivingNameFive'] = names.five || null;
    this.form['LivingNameSix'] = names.six || null;
  }

  onMailChange(sel: ZipcodeSelection): void {
    this.form['MailZipcodeID'] = sel.zipcodeId;
    this.form['MailZipcode'] = sel.zipcode;
    this.form['MailAddress'] = sel.address;
  }

  onTextChange(sel: ZipcodeSelection): void {
    this.form['TextZipcodeID'] = sel.zipcodeId;
    this.form['TextZipcode'] = sel.zipcode;
    this.form['TextAddress'] = sel.address;
  }

  async onSave(): Promise<void> {
    if (!this.form['CeremonyCategoryID']) { this.formError.set('請選擇法會類別'); return; }
    if (!this.form['Name']) { this.formError.set('請輸入姓名'); return; }

    this.saving.set(true);
    this.formError.set('');

    const data = {
      ...this.form,
      SignupID: crypto.randomUUID(),
      AdminID: this.auth.session()?.AdminID,
      Createdate: new Date().toISOString(),
    };

    const result = await this.svc.create(data);
    this.saving.set(false);

    if (result.success) {
      this.router.navigate(['/signups']);
    } else {
      this.formError.set(result.message || '儲存失敗');
    }
  }
}
