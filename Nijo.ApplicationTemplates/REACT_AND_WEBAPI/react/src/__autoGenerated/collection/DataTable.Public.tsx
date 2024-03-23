import React from 'react'
import * as RT from '@tanstack/react-table'
import * as Tree from '../util'
import { CustomComponentRef } from '../input'

export type DataTableProps<T> = {
  data?: T[]
  onChangeRow?: (index: number, data: T) => void
  columns?: ColumnDefEx<Tree.TreeNode<T>>[]
  className?: string
  treeView?: Tree.ToTreeArgs<T> & {
    rowHeader: (row: T) => React.ReactNode
  }
}
export type ColumnDefEx<TRow, TValue = any> = RT.ColumnDef<TRow> & {
  hidden?: boolean
  headerGroupName?: string
} & ({
  cellEditor?: never
  setValue?: never
} | {
  cellEditor: CellEditor<TValue>
  setValue: (data: TRow, value: TValue) => void
})

export type CellEditor<TValue> = (
  props: CellEditorProps<TValue>,
  ref: React.Ref<CustomComponentRef<TValue>>
) => JSX.Element

export type CellEditorProps<TValue> = {
  value: TValue | undefined
  onChange: (value: TValue | undefined) => void
  onKeyDown: React.KeyboardEventHandler<HTMLElement>
  onBlur: React.FocusEventHandler<HTMLElement>
  className: string
}

export type DataTableRef<T> = {
  getSelectedRows: () => { row: T, rowIndex: number }[]
  getSelectedItems: () => T[]
  getSelectedIndexes: () => number[]
}
