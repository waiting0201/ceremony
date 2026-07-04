import {
  ChangeDetectionStrategy,
  Component,
  computed,
  input,
} from '@angular/core';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { inject } from '@angular/core';

export type IconName =
  | 'believer'
  | 'plus'
  | 'search'
  | 'download'
  | 'category'
  | 'printer'
  | 'settings'
  | 'home'
  | 'pencil'
  | 'trash'
  | 'history'
  | 'file-plus'
  | 'insert-above'
  | 'more'
  | 'close'
  | 'database'
  | 'chevron-left';

const ICONS: Record<IconName, string> = {
  believer: `
    <circle cx="12" cy="8" r="4"></circle>
    <path d="M4 21c0-4 4-6 8-6s8 2 8 6"></path>`,
  plus: `
    <path d="M12 5v14"></path>
    <path d="M5 12h14"></path>`,
  search: `
    <circle cx="11" cy="11" r="7"></circle>
    <path d="m20 20-3.5-3.5"></path>`,
  download: `
    <path d="M12 3v12"></path>
    <path d="m6 11 6 6 6-6"></path>
    <path d="M5 21h14"></path>`,
  category: `
    <path d="M3 7h18"></path>
    <path d="M3 12h18"></path>
    <path d="M3 17h18"></path>`,
  printer: `
    <path d="M6 9V3h12v6"></path>
    <rect x="3" y="9" width="18" height="9" rx="1"></rect>
    <rect x="7" y="14" width="10" height="6"></rect>`,
  settings: `
    <circle cx="12" cy="12" r="3"></circle>
    <path d="M19.4 15a1.7 1.7 0 0 0 .3 1.9l.1.1a2 2 0 1 1-2.9 2.9l-.1-.1a1.7 1.7 0 0 0-1.9-.3 1.7 1.7 0 0 0-1 1.5V21a2 2 0 0 1-4 0v-.1a1.7 1.7 0 0 0-1-1.5 1.7 1.7 0 0 0-1.9.3l-.1.1a2 2 0 1 1-2.9-2.9l.1-.1a1.7 1.7 0 0 0 .3-1.9 1.7 1.7 0 0 0-1.5-1H3a2 2 0 0 1 0-4h.1a1.7 1.7 0 0 0 1.5-1 1.7 1.7 0 0 0-.3-1.9l-.1-.1a2 2 0 1 1 2.9-2.9l.1.1a1.7 1.7 0 0 0 1.9.3h.1a1.7 1.7 0 0 0 1-1.5V3a2 2 0 0 1 4 0v.1a1.7 1.7 0 0 0 1 1.5h.1a1.7 1.7 0 0 0 1.9-.3l.1-.1a2 2 0 1 1 2.9 2.9l-.1.1a1.7 1.7 0 0 0-.3 1.9v.1a1.7 1.7 0 0 0 1.5 1H21a2 2 0 0 1 0 4h-.1a1.7 1.7 0 0 0-1.5 1Z"></path>`,
  home: `
    <path d="m3 11 9-8 9 8"></path>
    <path d="M5 10v10h14V10"></path>`,
  pencil: `
    <path d="M12 20h9"></path>
    <path d="M16.5 3.5a2.121 2.121 0 1 1 3 3L7 19l-4 1 1-4 12.5-12.5Z"></path>`,
  trash: `
    <path d="M3 6h18"></path>
    <path d="M8 6V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"></path>
    <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6"></path>
    <path d="M10 11v6"></path>
    <path d="M14 11v6"></path>`,
  history: `
    <path d="M3 12a9 9 0 1 0 3-6.7"></path>
    <path d="M3 4v5h5"></path>
    <path d="M12 7v5l3 2"></path>`,
  'file-plus': `
    <path d="M14 3H6a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V9Z"></path>
    <path d="M14 3v6h6"></path>
    <path d="M12 13v6"></path>
    <path d="M9 16h6"></path>`,
  'insert-above': `
    <path d="M3 5h18"></path>
    <path d="M12 10v9"></path>
    <path d="m8 15 4 4 4-4"></path>`,
  more: `
    <circle cx="12" cy="5" r="1.4"></circle>
    <circle cx="12" cy="12" r="1.4"></circle>
    <circle cx="12" cy="19" r="1.4"></circle>`,
  close: `
    <path d="M18 6 6 18"></path>
    <path d="m6 6 12 12"></path>`,
  database: `
    <ellipse cx="12" cy="5" rx="8" ry="3"></ellipse>
    <path d="M4 5v6c0 1.66 3.58 3 8 3s8-1.34 8-3V5"></path>
    <path d="M4 11v6c0 1.66 3.58 3 8 3s8-1.34 8-3v-6"></path>`,
  'chevron-left': `
    <path d="m15 18-6-6 6-6"></path>`,
};

@Component({
  selector: 'app-icon',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <svg
      xmlns="http://www.w3.org/2000/svg"
      [attr.width]="size()"
      [attr.height]="size()"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      stroke-width="1.75"
      stroke-linecap="round"
      stroke-linejoin="round"
      aria-hidden="true"
      [innerHTML]="svgBody()"
    ></svg>
  `,
  styles: `
    :host {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      line-height: 1;
      color: inherit;
    }
  `,
})
export class IconComponent {
  private readonly sanitizer = inject(DomSanitizer);

  readonly name = input.required<IconName>();
  readonly size = input<number>(20);

  protected readonly svgBody = computed<SafeHtml>(() =>
    this.sanitizer.bypassSecurityTrustHtml(ICONS[this.name()]),
  );
}
