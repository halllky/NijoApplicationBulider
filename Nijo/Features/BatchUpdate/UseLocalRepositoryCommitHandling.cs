using Nijo.Core;
using Nijo.Parts.WebClient;
using Nijo.Util.CodeGenerating;
using Nijo.Util.DotnetEx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nijo.Features.BatchUpdate {
    partial class BatchUpdateFeature {

        internal static SourceFile UseLocalRepositoryCommitHandling(CodeRenderingContext context) {

            var aggregates = context.Schema
                .RootAggregatesOrderByDataFlow()
                .Where(agg => agg.Item.Options.Handler == NijoCodeGenerator.Models.WriteModel.Key);

            static string DeleteKeyUrlParam(GraphNode<Aggregate> agg) {
                return agg
                    .GetKeys()
                    .OfType<AggregateMember.ValueMember>()
                    .Select(vm => $"${{localReposItem.item.{vm.Declared.GetFullPath().Join("?.")}}}")
                    .Join("/");
            }

            return new SourceFile {
                FileName = "LocalRepository.Commit.ts",
                RenderContent = context => $$"""
                    /**
                     * このファイルはソース自動生成によって上書きされます。
                     */
                    import { useCallback, useMemo } from 'react'
                    import { useMsgContext } from './Notification'
                    import { useHttpRequest } from './Http'
                    import { ItemKey, LocalRepositoryContextValue, LocalRepositoryStoredItem, SaveLocalItemHandler } from './LocalRepository'
                    import * as AggregateType from '../autogenerated-types'

                    export const useLocalRepositoryCommitHandling = () => {
                      const [, dispatchMsg] = useMsgContext()
                      const { post, httpDelete } = useHttpRequest()

                      const saveHandlerMap = useMemo(() => new Map<string, SaveLocalItemHandler>([
                    {{aggregates.SelectTextTemplate(agg => $$"""
                        ['{{LocalRepository.GetDataTypeKey(agg)}}', async (localReposItem: LocalRepositoryStoredItem<AggregateType.{{agg.Item.TypeScriptTypeName}}>) => {
                          if (localReposItem.state === '+') {
                            const url = `{{new Controller(agg.Item).CreateCommandApi}}`
                            const response = await post<AggregateType.{{agg.Item.TypeScriptTypeName}}>(url, localReposItem.item)
                            return { commit: response.ok }

                          } else if (localReposItem.state === '*') {
                            const url = `{{new Controller(agg.Item).UpdateCommandApi}}`
                            const response = await post<AggregateType.{{agg.Item.TypeScriptTypeName}}>(url, localReposItem.item)
                            return { commit: response.ok }
                    
                          } else if (localReposItem.state === '-') {
                            const url = `{{new Controller(agg.Item).DeleteCommandApi}}/{{DeleteKeyUrlParam(agg)}}`
                            const response = await httpDelete(url)
                            return { commit: response.ok }
                    
                          } else {
                            dispatchMsg(msg => msg.error(`'${localReposItem.itemKey}' の状態 '${localReposItem.state}' が不正です。`))
                            return { commit: false }
                          }
                        }],
                    """)}}
                      ]), [post, httpDelete, dispatchMsg])

                      return useCallback(async (
                        commit: LocalRepositoryContextValue['commit'],
                        ...keys: { dataTypeKey: string, itemKey: ItemKey }[]
                      ) => {
                        const success = await commit(async localReposItem => {
                          const handler = saveHandlerMap.get(localReposItem.dataTypeKey)
                          if (!handler) {
                            dispatchMsg(msg => msg.error(`データ型 '${localReposItem.dataTypeKey}' の保存処理が定義されていません。`))
                            return { commit: false }
                          }
                          return await handler(localReposItem)
                        }, ...keys)

                        dispatchMsg(msg => success
                          ? msg.info('保存しました。')
                          : msg.info('一部のデータの保存に失敗しました。'))

                      }, [saveHandlerMap, dispatchMsg])
                    }
                    """,
            };
        }

    }
}
