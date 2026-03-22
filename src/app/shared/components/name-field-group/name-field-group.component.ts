import { Component, input, output, signal, effect } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

export interface NameFields {
  one: string;
  two: string;
  three: string;
  four: string;
  five: string;
  six: string;
}

@Component({
  selector: 'app-name-field-group',
  standalone: true,
  imports: [FormsModule, MatFormFieldModule, MatInputModule],
  template: `
    <div>
      <div class="mb-1 text-sm font-medium text-gray-800">{{ label() }}</div>
      <div class="grid grid-cols-3 gap-2">
        @for (i of indices; track i) {
          <mat-form-field appearance="outline">
            <mat-label>{{ label() }}{{ i }}</mat-label>
            <input matInput [ngModel]="values()[i - 1]"
                   (ngModelChange)="onFieldChange(i - 1, $event)" />
          </mat-form-field>
        }
      </div>
    </div>
  `,
})
export class NameFieldGroupComponent {
  label = input<string>('姓名');
  initialValues = input<string[]>([]);

  valuesChange = output<NameFields>();

  values = signal<string[]>(['', '', '', '', '', '']);
  indices = [1, 2, 3, 4, 5, 6];

  constructor() {
    effect(() => {
      const init = this.initialValues();
      if (init.length > 0) {
        const arr = [...init];
        while (arr.length < 6) arr.push('');
        this.values.set(arr);
      }
    });
  }

  onFieldChange(index: number, value: string): void {
    const current = [...this.values()];
    current[index] = value;
    this.values.set(current);
    this.valuesChange.emit({
      one: current[0],
      two: current[1],
      three: current[2],
      four: current[3],
      five: current[4],
      six: current[5],
    });
  }
}
