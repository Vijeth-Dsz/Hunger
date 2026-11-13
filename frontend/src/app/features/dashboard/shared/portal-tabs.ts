import { Component, EventEmitter, Input, Output } from '@angular/core';

export type PortalTabIcon = 'overview' | 'create' | 'donations' | 'community' | 'requests';

export interface PortalTab {
  id: string;
  label: string;
  icon: PortalTabIcon;
}

@Component({
  selector: 'app-portal-tabs',
  template: `
    <nav class="portal-tabs" role="tablist" [attr.aria-label]="label">
      @for (tab of tabs; track tab.id) {
        <button
          type="button"
          role="tab"
          [attr.aria-selected]="activeTab === tab.id"
          [class.active]="activeTab === tab.id"
          (click)="tabChange.emit(tab.id)">
          <svg class="tab-icon" viewBox="0 0 24 24" aria-hidden="true" focusable="false">
            <path [attr.d]="iconPaths[tab.icon]" />
          </svg>
          <span>{{ tab.label }}</span>
        </button>
      }
    </nav>
  `,
  styles: [`
    :host {
      display: block;
    }

    .portal-tabs {
      position: sticky;
      top: 0;
      z-index: 2;
      display: flex;
      flex-wrap: wrap;
      gap: 7px;
      padding: 10px 0;
      border-bottom: 1px solid var(--line);
      background: rgba(255, 255, 255, .96);
    }

    .portal-tabs button {
      display: inline-flex;
      min-height: 42px;
      align-items: center;
      gap: 9px;
      padding: 8px 14px;
      border: 1px solid transparent;
      border-radius: 10px;
      background: transparent;
      color: #627168;
      font: inherit;
      font-size: 13px;
      font-weight: 600;
      cursor: pointer;
      transition: background-color 140ms ease, border-color 140ms ease, color 140ms ease;
    }

    .tab-icon {
      width: 17px;
      height: 17px;
      flex: 0 0 auto;
      fill: none;
      stroke: currentColor;
      stroke-linecap: round;
      stroke-linejoin: round;
      stroke-width: 1.7;
    }

    .portal-tabs button:hover,
    .portal-tabs button:focus-visible,
    .portal-tabs button.active {
      border-color: #e2ebe2;
      background: #f1f6f0;
      color: var(--green);
      outline: none;
    }

    .portal-tabs button.active {
      box-shadow: inset 0 -2px 0 var(--green);
    }

    @media (max-width: 520px) {
      .portal-tabs {
        position: static;
        gap: 3px;
      }

      .portal-tabs button {
        min-height: 42px;
        gap: 7px;
        padding: 8px 11px;
        font-size: 12px;
        white-space: nowrap;
      }

      .tab-icon {
        width: 15px;
        height: 15px;
      }
    }
  `],
})
export class PortalTabs {
  readonly iconPaths: Record<PortalTabIcon, string> = {
    overview: 'M3 3h8v8H3zM13 3h8v5h-8zM13 10h8v11h-8zM3 13h8v8H3z',
    create: 'M12 5v14M5 12h14',
    donations: 'm3 10 9-7 9 7v10H3V10Zm5 10v-6h8v6',
    community: 'M12 21s-8-4.5-8-10a4.5 4.5 0 0 1 8-2.8A4.5 4.5 0 0 1 20 11c0 5.5-8 10-8 10Z',
    requests: 'M6 4h12v17H6zM9 4V2h6v2M9 10h6M9 14h6M9 18h4',
  };

  @Input() tabs: PortalTab[] = [];
  @Input() activeTab = 'overview';
  @Input() label = '';
  @Output() tabChange = new EventEmitter<string>();
}
