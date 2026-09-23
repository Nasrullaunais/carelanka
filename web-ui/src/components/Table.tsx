import type { ComponentProps, ReactNode } from 'react';

type TableProps = ComponentProps<'table'> & {
  /** A visible or screen-reader-only name for the table. */
  caption?: ReactNode;
};

/** Shared frame and horizontal overflow for every data table. */
export function Table({ caption, className, children, ...props }: TableProps) {
  return (
    <div className="table-frame">
      <div className="table-scroll">
        <table className={['app-table', className].filter(Boolean).join(' ')} {...props}>
          {caption && <caption>{caption}</caption>}
          {children}
        </table>
      </div>
    </div>
  );
}
