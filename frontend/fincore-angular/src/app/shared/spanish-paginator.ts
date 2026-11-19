import { MatPaginatorIntl } from '@angular/material/paginator';

/** Textos del paginador de Material en español. */
export function spanishPaginator(): MatPaginatorIntl {
  const intl = new MatPaginatorIntl();
  intl.itemsPerPageLabel = 'Filas por página';
  intl.nextPageLabel = 'Siguiente';
  intl.previousPageLabel = 'Anterior';
  intl.firstPageLabel = 'Primera página';
  intl.lastPageLabel = 'Última página';
  intl.getRangeLabel = (page, pageSize, length) => {
    if (length === 0) return '0 de 0';
    const start = page * pageSize + 1;
    const end = Math.min(start + pageSize - 1, length);
    return `${start} – ${end} de ${length}`;
  };
  return intl;
}
