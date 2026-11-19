import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { OperationsByPeriodRow } from '../../core/api/api.models';

interface Bar {
  label: string;
  inflow: number;
  outflow: number;
}

/**
 * Entradas y salidas efectivas por período (columnas agrupadas). SVG propio, sin librerías de gráficos:
 * pesa casi nada y usa los colores del tema (salvia = entra, rosa = sale). Las reversiones se excluyen.
 */
@Component({
  selector: 'fc-period-chart',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (bars().length) {
      <svg [attr.viewBox]="'0 0 ' + width + ' ' + height" role="img" [attr.aria-label]="ariaLabel()">
        @for (tick of ticks(); track tick.value) {
          <line [attr.x1]="padLeft" [attr.x2]="width - 8" [attr.y1]="tick.y" [attr.y2]="tick.y" class="grid" />
          <text [attr.x]="padLeft - 8" [attr.y]="tick.y + 4" class="axis" text-anchor="end">{{ tick.label }}</text>
        }
        @for (bar of bars(); track bar.label; let i = $index) {
          <g [attr.transform]="'translate(' + x(i) + ',0)'">
            <rect class="in" [attr.x]="0" [attr.y]="y(bar.inflow)" [attr.width]="barWidth()" [attr.height]="h(bar.inflow)" rx="4">
              <title>{{ bar.label }} · entradas {{ money(bar.inflow) }}</title>
            </rect>
            <rect class="out" [attr.x]="barWidth() + 3" [attr.y]="y(bar.outflow)" [attr.width]="barWidth()" [attr.height]="h(bar.outflow)" rx="4">
              <title>{{ bar.label }} · salidas {{ money(bar.outflow) }}</title>
            </rect>
            <text class="axis" [attr.x]="barWidth() + 1.5" [attr.y]="height - 8" text-anchor="middle">{{ bar.label }}</text>
          </g>
        }
      </svg>
      <div class="legend"><span class="dot in"></span> Entradas <span class="dot out"></span> Salidas</div>
    } @else {
      <p class="empty">Sin operaciones efectivas en el período.</p>
    }
  `,
  styles: `
    :host { display: block; overflow-x: auto; }
    svg { width: 100%; min-width: 560px; height: auto; display: block; }
    .grid { stroke: var(--fc-border); stroke-dasharray: 3 4; }
    .axis { font-size: 11px; fill: var(--fc-text-muted); font-family: var(--fc-font); }
    rect { transition: opacity .2s; } rect:hover { opacity: .8; }
    .in { fill: var(--fc-sage); } .out { fill: var(--fc-blush); }
    .legend { display: flex; gap: 14px; align-items: center; font-size: 12.5px; color: var(--fc-text-muted); margin-top: 6px; }
    .dot { display: inline-block; width: 10px; height: 10px; border-radius: 3px; margin-right: -8px; }
    .dot.in { background: var(--fc-sage); } .dot.out { background: var(--fc-blush); }
    .empty { color: var(--fc-text-muted); text-align: center; padding: 40px 0; margin: 0; }
  `,
})
export class PeriodChartComponent {
  readonly rows = input.required<OperationsByPeriodRow[]>();
  readonly granularity = input<'DAY' | 'MONTH'>('DAY');

  protected readonly width = 760;
  protected readonly height = 260;
  protected readonly padLeft = 64;
  private readonly padTop = 12;
  private readonly padBottom = 28;

  protected readonly bars = computed<Bar[]>(() => {
    const byPeriod = new Map<string, Bar>();
    for (const r of this.rows()) {
      if (r.transactionTypeCode === 'REV') continue;
      const bar = byPeriod.get(r.periodStart) ?? { label: this.label(r.periodStart), inflow: 0, outflow: 0 };
      if (r.transactionTypeCode === 'ING') bar.inflow += r.effectiveAmount;
      else bar.outflow += r.effectiveAmount;
      byPeriod.set(r.periodStart, bar);
    }
    return [...byPeriod.entries()].sort(([a], [b]) => a.localeCompare(b)).map(([, bar]) => bar);
  });

  private readonly max = computed(() => Math.max(1, ...this.bars().flatMap((b) => [b.inflow, b.outflow])));

  protected readonly ticks = computed(() =>
    [0, 0.25, 0.5, 0.75, 1].map((f) => ({ value: f, y: this.y(this.max() * f), label: this.short(this.max() * f) })),
  );

  protected readonly barWidth = computed(() => {
    const slot = (this.width - this.padLeft - 16) / Math.max(1, this.bars().length);
    return Math.max(4, Math.min(26, slot / 2 - 4));
  });

  protected readonly ariaLabel = computed(() => `Entradas y salidas en ${this.bars().length} períodos`);

  protected x(i: number): number {
    const slot = (this.width - this.padLeft - 16) / Math.max(1, this.bars().length);
    return this.padLeft + i * slot + slot / 2 - this.barWidth();
  }

  protected y(value: number): number {
    const usable = this.height - this.padTop - this.padBottom;
    return this.padTop + usable - (value / this.max()) * usable;
  }

  protected h(value: number): number {
    return Math.max(0, this.height - this.padBottom - this.y(value));
  }

  protected money(v: number): string {
    return v.toLocaleString('es-CO', { style: 'currency', currency: 'COP', maximumFractionDigits: 0 });
  }

  private short(v: number): string {
    return v >= 1e6 ? `${(v / 1e6).toLocaleString('es-CO', { maximumFractionDigits: 1 })} M` : v >= 1e3 ? `${Math.round(v / 1e3)} mil` : '0';
  }

  private label(iso: string): string {
    const [y, m, d] = iso.split('-').map(Number);
    const date = new Date(y, m - 1, d);
    return this.granularity() === 'MONTH'
      ? date.toLocaleDateString('es-CO', { month: 'short', year: '2-digit' })
      : date.toLocaleDateString('es-CO', { day: 'numeric', month: 'short' });
  }
}
