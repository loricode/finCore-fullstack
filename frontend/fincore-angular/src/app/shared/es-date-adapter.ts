import { Injectable } from '@angular/core';
import { NativeDateAdapter } from '@angular/material/core';

/**
 * Adaptador de fechas para Colombia: lee y muestra dd/MM/yyyy.
 * El NativeDateAdapter de Material usa Date.parse al escribir a mano, que interpreta "1/11/2026" como 11 de enero
 * (formato de EE. UU.). Aquí se interpreta día/mes/año, como lo escribe un usuario colombiano.
 */
@Injectable()
export class EsDateAdapter extends NativeDateAdapter {
  private static readonly DayFirst = /^(\d{1,2})[/.-](\d{1,2})[/.-](\d{4})$/;

  override parse(value: unknown, parseFormat?: object): Date | null {
    if (typeof value === 'string') {
      const text = value.trim();
      if (!text) return null;
      const m = EsDateAdapter.DayFirst.exec(text);
      if (m) {
        const [day, month, year] = [Number(m[1]), Number(m[2]), Number(m[3])];
        const date = new Date(year, month - 1, day);
        // Rechaza fechas imposibles (31/02/2026) en lugar de "corregirlas" al mes siguiente.
        return date.getFullYear() === year && date.getMonth() === month - 1 && date.getDate() === day ? date : this.invalid();
      }
    }
    return super.parse(value, parseFormat);
  }

  override format(date: Date, displayFormat: object): string {
    const f = displayFormat as Intl.DateTimeFormatOptions;
    if (f?.day === 'numeric' && f?.month === 'numeric' && f?.year === 'numeric') {
      const p = (n: number) => String(n).padStart(2, '0');
      return `${p(date.getDate())}/${p(date.getMonth() + 1)}/${date.getFullYear()}`;
    }
    return super.format(date, displayFormat);
  }
}
