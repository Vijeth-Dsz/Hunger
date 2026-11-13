import { Component, Input } from '@angular/core';

@Component({
  selector: 'app-expandable-record',
  template: `
    <details class="expandable-record">
      <summary>
        <span class="record-heading">
          <span class="record-category">{{ category }}</span>
          <strong>{{ title }}</strong>
        </span>
        <span class="record-summary-meta">
          <span class="status-pill" [attr.data-status]="status">{{ statusLabel }}</span>
          <span class="expand-indicator" aria-hidden="true"></span>
        </span>
      </summary>
      <div class="record-content">
        <ng-content />
      </div>
    </details>
  `,
  styles: [`
    :host {
      display: block;
      min-width: 0;
    }

    .expandable-record {
      overflow: hidden;
      border: 1px solid var(--line);
      border-radius: 10px;
      background: #fff;
    }

    summary {
      display: flex;
      min-height: 60px;
      justify-content: space-between;
      align-items: center;
      gap: 14px;
      padding: 12px 15px;
      cursor: pointer;
      list-style: none;
    }

    summary::-webkit-details-marker {
      display: none;
    }

    summary:focus-visible {
      outline: 2px solid #8ba894;
      outline-offset: -2px;
    }

    .record-heading {
      display: grid;
      min-width: 0;
      gap: 4px;
    }

    .record-category {
      color: #718078;
      font-size: 9px;
      font-weight: 700;
      letter-spacing: .7px;
      text-transform: uppercase;
    }

    .record-heading strong {
      overflow: hidden;
      color: var(--ink);
      font-size: 12px;
      text-overflow: ellipsis;
    }

    .record-summary-meta {
      display: flex;
      flex: 0 0 auto;
      align-items: center;
      gap: 12px;
    }

    .status-pill {
      padding: 5px 8px;
      border-radius: 20px;
      background: #edf3ee;
      color: #52705c;
      font-size: 9px;
      white-space: nowrap;
    }

    .status-pill[data-status="Pending"],
    .status-pill[data-status="PendingReview"] {
      background: #fff4df;
      color: #96712f;
    }

    .status-pill[data-status="Rejected"],
    .status-pill[data-status="Cancelled"] {
      background: #fbefed;
      color: #a1443c;
    }

    .expand-indicator::before {
      content: '+';
      color: #718078;
      font-size: 18px;
    }

    details[open] .expand-indicator::before {
      content: '−';
    }

    .record-content {
      padding: 0 15px 15px;
      border-top: 1px solid var(--line);
    }

    @media (max-width: 520px) {
      summary {
        align-items: flex-start;
        padding: 12px;
      }

      .record-summary-meta {
        gap: 7px;
      }

      .record-content {
        padding: 0 12px 12px;
      }
    }
  `],
})
export class ExpandableRecord {
  @Input({ required: true }) title = '';
  @Input() category = '';
  @Input() status = '';
  @Input() statusLabel = '';
}
