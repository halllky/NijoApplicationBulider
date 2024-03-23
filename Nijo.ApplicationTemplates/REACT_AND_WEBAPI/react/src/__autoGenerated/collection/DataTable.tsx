import React, { useCallback, useImperativeHandle, useMemo, useRef, useState } from 'react'
import * as RT from '@tanstack/react-table'
import * as Util from '../util'
import * as Tree from '../util'
import { ColumnDefEx, DataTableProps, DataTableRef } from './DataTable.Public'
import { getRowHeader, ROW_HEADER_ID, TABLE_ZINDEX } from './DataTable.Parts'
import { useCellEditing } from './DataTable.Editing'
import { useSelection } from './DataTable.Selecting'
import { COLUMN_RESIZE_OPTION, useColumnResizing } from './DataTable.ColResize'

export * from './DataTable.Public'

export const DataTable = Util.forwardRefEx(<T,>(props: DataTableProps<T>, ref: React.ForwardedRef<DataTableRef<T>>) => {
  const {
    data,
    columns: propsColumns,
    treeView,
    className,
  } = props

  // 行
  const dataAsTree = useMemo(() => {
    if (!data) return []
    return Tree.toTree(data, treeView)
  }, [data, treeView])

  // 列
  const columnHelper = useMemo(() => RT.createColumnHelper<Tree.TreeNode<T>>(), [])
  const columns: RT.ColumnDef<Tree.TreeNode<T>>[] = useMemo(() => {
    const result: RT.ColumnDef<Tree.TreeNode<T>>[] = []
    if (treeView) result.unshift(getRowHeader(columnHelper, treeView))
    const colgroups = Util.groupBy(propsColumns ?? [], col => col.headerGroupName ?? '')
    for (const [header, columns] of colgroups) {
      if (header) {
        result.push(columnHelper.group({ header, columns }))
      } else {
        result.push(...columns)
      }
    }
    return result
  }, [propsColumns, columnHelper, treeView])

  // 表
  const optoins: RT.TableOptions<Tree.TreeNode<T>> = useMemo(() => ({
    data: dataAsTree,
    columns,
    getSubRows: row => row.children,
    getCoreRowModel: RT.getCoreRowModel(),
    ...COLUMN_RESIZE_OPTION,
  }), [dataAsTree, columns])

  const api = RT.useReactTable(optoins)

  const {
    editing,
    startEditing,
    cancelEditing,
    CellEditor,
    cellEditorProps,
    editingTdRefCallback,
  } = useCellEditing<T>(props)

  const {
    caretCell,
    selectObject,
    handleSelectionKeyDown,
    caretTdRefCallback,
    ActiveCellBorder,
    activeCellBorderProps,
    getSelectedRows,
    getSelectedIndexes,
  } = useSelection<T>(editing, api)

  const {
    columnSizeVars,
    getColWidth,
    ResizeHandler,
  } = useColumnResizing(api)

  const tdRefCallback = (td: HTMLTableCellElement | null, cell: RT.Cell<Tree.TreeNode<T>, unknown>) => {
    caretTdRefCallback(td, cell)
    editingTdRefCallback(td, cell)
  }

  const [isActive, setIsActive] = useState(false)
  const handleFocus: React.FocusEventHandler<HTMLDivElement> = useCallback(() => {
    setIsActive(true)
    if (!caretCell) selectObject({ any: api })
  }, [api, caretCell, selectObject])
  const handleBlur: React.FocusEventHandler<HTMLDivElement> = useCallback(e => {
    // フォーカスの移動先がこの要素の中にある場合はfalseにしない
    if (!e.target.contains(e.relatedTarget)) setIsActive(false)
  }, [])

  // セル編集完了時にフォーカスが外れてキー操作ができなくなるのを防ぐ
  const containerRef = useRef<HTMLDivElement>(null)
  const onEndEditing = useCallback(() => {
    setTimeout(() => {
      containerRef.current?.focus()
    }, 10)
  }, [])

  const handleKeyDown: React.KeyboardEventHandler<HTMLDivElement> = useCallback(e => {
    // console.log(e.key)
    if (e.key === ' ') {
      for (const row of getSelectedRows()) row.toggleExpanded()
      e.preventDefault()
      return
    } else if (e.key === 'Escape' && editing) {
      cancelEditing()
      e.preventDefault()
      return
    } else if (e.key === 'F2' && !editing && caretCell) {
      // caretCellは更新前の古いセルなので最新の配列から検索しなおす TODO:caretCellのidだけをstateにもつようにする
      const row = api.getRow(caretCell.row.id)
      const cell = row.getAllCells().find(x => x.id === caretCell.id)
      if (cell) startEditing(cell)
      e.preventDefault()
      return
    }

    handleSelectionKeyDown(e)
    if (e.defaultPrevented) return
  }, [api, editing, caretCell, getSelectedRows, handleSelectionKeyDown, startEditing, cancelEditing])

  useImperativeHandle(ref, () => ({
    getSelectedRows: () => getSelectedRows().map(row => ({
      row: row.original.item,
      rowIndex: row.index,
    })),
    // TODO: ↓いらない
    getSelectedItems: () => getSelectedRows().map(row => row.original.item),
    getSelectedIndexes,
  }), [getSelectedRows])

  return (
    <div ref={containerRef}
      className={`outline-none overflow-auto select-none relative bg-color-2 border border-1 border-color-4 ${className}`}
      onFocus={handleFocus}
      onBlur={handleBlur}
      onKeyDown={handleKeyDown}
      tabIndex={0}
    >
      <table
        className="table-fixed border-separate border-spacing-0 border-b border-1 border-color-4"
        style={{ ...columnSizeVars, width: api.getTotalSize() }}
      >
        {/* ヘッダ */}
        <thead>
          {api.getHeaderGroups().map(headerGroup => (
            <tr key={headerGroup.id}>

              {headerGroup.headers.filter(h => !(h.column.columnDef as ColumnDefEx<T>).hidden).map(header => (
                <th key={header.id}
                  colSpan={header.colSpan}
                  className="relative overflow-hidden px-1 py-0 text-start bg-color-3"
                  style={{ width: getColWidth(header), ...getThStickeyStyle(header) }}>
                  {!header.isPlaceholder && RT.flexRender(
                    header.column.columnDef.header,
                    header.getContext())}
                  <ResizeHandler header={header} />
                </th>
              ))}

            </tr>
          ))}
        </thead>

        {/* ボディ */}
        <tbody className="bg-color-0">
          {api.getRowModel().flatRows.filter(row => row.getIsAllParentsExpanded()).map(row => (
            <tr
              key={row.id}
              className="leading-tight"
            >
              {row.getVisibleCells().filter(c => !(c.column.columnDef as ColumnDefEx<T>).hidden).map(cell => (
                <td key={cell.id}
                  ref={td => tdRefCallback(td, cell)}
                  className="relative overflow-hidden p-0 border-r border-1 border-color-4"
                  style={getTdStickeyStyle(cell)}
                  onMouseDown={e => selectObject({ cell, shiftKey: e.shiftKey })}
                  onDoubleClick={() => startEditing(cell)}
                >
                  {RT.flexRender(
                    cell.column.columnDef.cell,
                    cell.getContext())}
                </td>
              ))}

            </tr>
          ))}
        </tbody>
      </table>

      {isActive && !editing && (
        <ActiveCellBorder api={api} {...activeCellBorderProps} />
      )}
      {editing && (
        <CellEditor onEndEditing={onEndEditing} {...cellEditorProps} />
      )}
    </div>
  )
})

// -----------------------------------------------
// 行列ヘッダ固定
const getThStickeyStyle = (header: RT.Header<any, unknown>): React.CSSProperties => ({
  position: 'sticky',
  top: 0,
  left: header.column.id === ROW_HEADER_ID ? 0 : undefined,
  zIndex: header.column.id === ROW_HEADER_ID ? TABLE_ZINDEX.ROWHEADER_THEAD : TABLE_ZINDEX.THEAD,
})
const getTdStickeyStyle = (cell: RT.Cell<any, unknown>): React.CSSProperties => ({
  position: cell.column.id === ROW_HEADER_ID ? 'sticky' : undefined,
  left: cell.column.id === ROW_HEADER_ID ? 0 : undefined,
  zIndex: cell.column.id === ROW_HEADER_ID ? TABLE_ZINDEX.ROWHEADER : undefined,
})
