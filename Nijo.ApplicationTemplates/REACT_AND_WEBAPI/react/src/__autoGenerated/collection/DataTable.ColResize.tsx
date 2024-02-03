import { useMemo, useCallback } from 'react'
import * as RT from '@tanstack/react-table'
import * as Tree from '../util'

export const COLUMN_RESIZE_OPTION: Partial<RT.TableOptions<Tree.TreeNode<any>>> = {
  defaultColumn: {
    minSize: 60,
    maxSize: 800,
  },
  columnResizeMode: 'onChange',
}

export const useColumnResizing = <T,>(api: RT.Table<Tree.TreeNode<T>>) => {

  const columnSizeVars = useMemo(() => {
    const headers = api.getFlatHeaders()
    const colSizes: { [key: string]: number } = {}
    for (let i = 0; i < headers.length; i++) {
      const header = headers[i]!
      colSizes[`--header-${header.id}-size`] = header.getSize()
      colSizes[`--col-${header.column.id}-size`] = header.column.getSize()
    }
    return colSizes
  }, [api.getState().columnSizingInfo])

  const getColWidth = useCallback((header: RT.Header<Tree.TreeNode<T>, unknown>) => {
    return `calc(var(--header-${header?.id}-size) * 1px)`
  }, [])

  const ResizeHandler = useCallback(({ header }: {
    header: RT.Header<Tree.TreeNode<T>, unknown>
  }) => {
    return (
      <div {...{
        onDoubleClick: () => header.column.resetSize(),
        onMouseDown: header.getResizeHandler(),
        onTouchStart: header.getResizeHandler(),
        className: `absolute top-0 bottom-0 right-0 w-3 cursor-ew-resize border-r border-color-4`,
      }}>
      </div>
    )
  }, [])

  return {
    columnSizeVars,
    getColWidth,
    ResizeHandler,
  }
}