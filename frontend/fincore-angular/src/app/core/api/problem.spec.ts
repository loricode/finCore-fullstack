import { HttpErrorResponse } from '@angular/common/http';
import { FormControl, FormGroup } from '@angular/forms';
import { spanishPaginator } from '../../shared/spanish-paginator';
import { applyFieldErrors, describeError } from './problem';

describe('Errores de la API', () => {
  it('describe un problem+json con su correlationId y errores por campo', () => {
    const error = new HttpErrorResponse({
      status: 400,
      error: { title: 'Datos inválidos', detail: 'Revise los campos', correlationId: 'abc', errors: { amount: ['Debe ser mayor que cero.'] } },
    });
    const d = describeError(error);
    expect(d.message).toBe('Revise los campos');
    expect(d.correlationId).toBe('abc');
    expect(d.fieldErrors?.['amount']).toEqual(['Debe ser mayor que cero.']);
  });

  it('pinta en el formulario los errores de cada campo y devuelve los que no tienen control', () => {
    const form = new FormGroup({ amount: new FormControl(0), reference: new FormControl('') });
    const unmatched = applyFieldErrors(form, { amount: ['Debe ser mayor que cero.'], branchIds: ['Asigna una sucursal.'] });

    expect(form.controls.amount.errors).toEqual({ server: 'Debe ser mayor que cero.' });
    expect(form.controls.amount.touched).toBe(true);
    expect(form.controls.reference.errors).toBeNull();
    expect(unmatched).toEqual(['Asigna una sucursal.']);
  });

  it('sin conexión muestra un mensaje claro', () => {
    expect(describeError(new HttpErrorResponse({ status: 0 })).message).toBe('No hay conexión con el servidor.');
  });
});

describe('Paginador en español', () => {
  it('muestra el rango en español', () => {
    const intl = spanishPaginator();
    expect(intl.getRangeLabel(1, 15, 40)).toBe('16 – 30 de 40');
    expect(intl.getRangeLabel(0, 15, 0)).toBe('0 de 0');
    expect(intl.itemsPerPageLabel).toBe('Filas por página');
  });
});
