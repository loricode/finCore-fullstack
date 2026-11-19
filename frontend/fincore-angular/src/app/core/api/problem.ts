import { HttpErrorResponse } from '@angular/common/http';
import { FormGroup } from '@angular/forms';
import { ProblemDetails } from '../auth/auth.models';

export interface DescribedError {
  message: string;
  correlationId?: string;
  fieldErrors?: Record<string, string[]>;
}

/** Mensaje legible de un error de la API (application/problem+json) con su correlationId para soporte. */
export function describeError(error: unknown): DescribedError {
  if (error instanceof HttpErrorResponse) {
    if (error.status === 0) return { message: 'No hay conexión con el servidor.' };
    if (error.status === 429) return { message: 'Demasiados intentos. Espera un minuto e intenta de nuevo.' };
    const problem = (typeof error.error === 'object' ? error.error : null) as ProblemDetails | null;
    if (problem?.detail || problem?.title) {
      return { message: problem.detail ?? problem.title!, correlationId: problem.correlationId, fieldErrors: problem.errors };
    }
    return { message: `Error ${error.status}.` };
  }
  return { message: 'Ocurrió un error inesperado.' };
}

/**
 * Pinta en el formulario los errores por campo que devolvió la API (400). Las claves de la API vienen en camelCase,
 * igual que los nombres de los controles. Devuelve los errores que no corresponden a ningún control.
 */
export function applyFieldErrors(form: FormGroup, errors: Record<string, string[]> | undefined): string[] {
  const unmatched: string[] = [];
  for (const [field, messages] of Object.entries(errors ?? {})) {
    const control = form.get(field);
    if (control) {
      control.setErrors({ server: messages[0] });
      control.markAsTouched();
    } else {
      unmatched.push(...messages);
    }
  }
  return unmatched;
}
