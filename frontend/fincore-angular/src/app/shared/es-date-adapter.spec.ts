import { TestBed } from '@angular/core/testing';
import { MAT_DATE_LOCALE } from '@angular/material/core';
import { EsDateAdapter } from './es-date-adapter';

describe('EsDateAdapter', () => {
  let adapter: EsDateAdapter;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [EsDateAdapter, { provide: MAT_DATE_LOCALE, useValue: 'es-CO' }] });
    adapter = TestBed.inject(EsDateAdapter);
  });

  it('lee día/mes/año como lo escribe un usuario en Colombia', () => {
    const date = adapter.parse('1/11/2026', {})!;
    expect([date.getFullYear(), date.getMonth(), date.getDate()]).toEqual([2026, 10, 1]); // 1 de noviembre
  });

  it('acepta guion y punto como separador', () => {
    expect(adapter.parse('15-11-2026', {})!.getDate()).toBe(15);
    expect(adapter.parse('15.11.2026', {})!.getMonth()).toBe(10);
  });

  it('rechaza fechas imposibles en lugar de moverlas al mes siguiente', () => {
    expect(adapter.isValid(adapter.parse('31/02/2026', {})!)).toBe(false);
  });

  it('muestra dd/MM/yyyy en el campo', () => {
    expect(adapter.format(new Date(2026, 10, 1), { year: 'numeric', month: 'numeric', day: 'numeric' })).toBe('01/11/2026');
  });
});
