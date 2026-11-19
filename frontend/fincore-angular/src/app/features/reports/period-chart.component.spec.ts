import { TestBed } from '@angular/core/testing';
import { OperationsByPeriodRow } from '../../core/api/api.models';
import { PeriodChartComponent } from './period-chart.component';

function row(periodStart: string, transactionTypeCode: string, effectiveAmount: number): OperationsByPeriodRow {
  return { periodStart, transactionTypeCode, effectiveAmount } as OperationsByPeriodRow;
}

describe('PeriodChartComponent', () => {
  function render(rows: OperationsByPeriodRow[]) {
    const fixture = TestBed.createComponent(PeriodChartComponent);
    fixture.componentRef.setInput('rows', rows);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('agrupa entradas y salidas por período, en orden, sin contar reversiones', () => {
    const el = render([
      row('2026-11-02', 'EGR', 300), row('2026-11-01', 'ING', 1000), row('2026-11-01', 'PAG', 200),
      row('2026-11-01', 'REV', 999), row('2026-11-02', 'ING', 50),
    ]);
    const titles = [...el.querySelectorAll('rect title')].map((t) => t.textContent!.replace(/\s/g, ' '));
    expect(titles).toHaveLength(4); // 2 períodos × (entra, sale)
    expect(titles[0]).toMatch(/^1 (de )?nov/);
    expect(titles[0]).toMatch(/entradas .*1\.000/);
    expect(titles[1]).toMatch(/salidas .*200/);
    expect(titles[3]).toMatch(/salidas .*300/);
  });

  it('muestra un mensaje amable cuando no hay datos', () => {
    expect(render([]).textContent).toContain('Sin operaciones efectivas');
  });
});
