import { Component, inject, input, output, signal, effect, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { IpcService } from '../../../core/services/ipc.service';

interface Zipcode {
  ZipcodeID: number;
  City: string;
  Area: string;
  Zipcode: string;
}

export interface ZipcodeSelection {
  zipcodeId: number | null;
  zipcode: string;
  city: string;
  area: string;
  address: string;
}

@Component({
  selector: 'app-zipcode-select',
  standalone: true,
  imports: [FormsModule, MatFormFieldModule, MatSelectModule],
  template: `
    <div class="grid grid-cols-4 gap-2">
      <mat-form-field appearance="outline">
        <mat-label>縣市</mat-label>
        <mat-select [(ngModel)]="selectedCity" (ngModelChange)="onCityChange($event)">
          @for (city of cities(); track city) {
            <mat-option [value]="city">{{ city }}</mat-option>
          }
        </mat-select>
      </mat-form-field>

      <mat-form-field appearance="outline">
        <mat-label>鄉鎮區</mat-label>
        <mat-select [(ngModel)]="selectedAreaId" (ngModelChange)="onAreaChange($event)">
          @for (area of areas(); track area.ZipcodeID) {
            <mat-option [value]="area.ZipcodeID">{{ area.Area }} ({{ area.Zipcode }})</mat-option>
          }
        </mat-select>
      </mat-form-field>

      <mat-form-field appearance="outline" class="col-span-2">
        <mat-label>地址</mat-label>
        <input matInput [(ngModel)]="addressText" (ngModelChange)="emitChange()" />
      </mat-form-field>
    </div>
  `,
})
export class ZipcodeSelectComponent implements OnInit {
  private ipc = inject(IpcService);

  /** Initial values */
  initialZipcodeId = input<number | null>(null);
  initialAddress = input<string>('');

  /** Output event */
  selectionChange = output<ZipcodeSelection>();

  cities = signal<string[]>([]);
  areas = signal<Zipcode[]>([]);
  selectedCity = '';
  selectedAreaId: number | null = null;
  addressText = '';

  private allZipcodes: Zipcode[] = [];

  constructor() {
    // When initialZipcodeId changes, restore selection
    effect(() => {
      const id = this.initialZipcodeId();
      const addr = this.initialAddress();
      if (id && this.allZipcodes.length > 0) {
        this.restoreSelection(id, addr);
      }
    });
  }

  async ngOnInit(): Promise<void> {
    // Load all zipcodes once
    const result = await this.ipc.invoke<Zipcode[]>('zipcodes:list');
    if (result.success && result.data) {
      this.allZipcodes = result.data;
      const citySet = new Set(result.data.map((z) => z.City));
      this.cities.set([...citySet].sort());

      // Restore if initial value exists
      const id = this.initialZipcodeId();
      if (id) {
        this.restoreSelection(id, this.initialAddress());
      }
    }
  }

  onCityChange(city: string): void {
    this.areas.set(this.allZipcodes.filter((z) => z.City === city));
    this.selectedAreaId = null;
    this.emitChange();
  }

  onAreaChange(_id: number): void {
    this.emitChange();
  }

  emitChange(): void {
    const area = this.allZipcodes.find((z) => z.ZipcodeID === this.selectedAreaId);
    this.selectionChange.emit({
      zipcodeId: this.selectedAreaId,
      zipcode: area?.Zipcode ?? '',
      city: area?.City ?? this.selectedCity,
      area: area?.Area ?? '',
      address: this.addressText,
    });
  }

  private restoreSelection(zipcodeId: number, address: string): void {
    const zip = this.allZipcodes.find((z) => z.ZipcodeID === zipcodeId);
    if (zip) {
      this.selectedCity = zip.City;
      this.areas.set(this.allZipcodes.filter((z) => z.City === zip.City));
      this.selectedAreaId = zip.ZipcodeID;
    }
    this.addressText = address;
  }
}
