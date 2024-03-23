export const groupBy = <TItem, TKey>(arr: TItem[], fn: (t: TItem) => TKey): Map<TKey, TItem[]> => {
  return arr.reduce((map, curr) => {
    const key = fn(curr)
    const group = map.get(key)
    if (group) {
      group.push(curr)
    } else {
      map.set(key, [curr])
    }
    return map
  }, new Map<TKey, TItem[]>())
}
