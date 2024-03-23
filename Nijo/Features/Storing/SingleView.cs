using Nijo.Core;
using Nijo.Util.DotnetEx;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nijo.Util.CodeGenerating;
using Nijo.Features;
using Nijo.Parts.WebServer;
using Nijo.Features.BatchUpdate;
using Nijo.Parts.WebClient;

namespace Nijo.Features.Storing {
    public class SingleView : IReactPage {
        public enum E_Type {
            Create,
            View,
            Edit,
        }

        internal SingleView(GraphNode<Aggregate> aggregate, E_Type type) {
            if (!aggregate.IsRoot()) throw new ArgumentException("Descendant aggregate cannot have SingleView.");
            _aggregate = aggregate;
            _type = type;
        }
        private readonly GraphNode<Aggregate> _aggregate;
        private readonly E_Type _type;

        private const string KEY0 = "key0"; // Createの場合、ローカルリポジトリのitemKeyをURLで受け取る

        string IReactPage.Url {
            get {
                if (_type == E_Type.Create) {
                    return $"/{_aggregate.Item.UniqueId}/new/:{KEY0}?";

                } else {
                    // React Router は全角文字非対応なので key0, key1, ... をURLに使う
                    var urlKeys = _aggregate
                        .GetKeys()
                        .OfType<AggregateMember.ValueMember>()
                        .Select((_, i) => $":key{i}");

                    if (_type == E_Type.View) {
                        return $"/{_aggregate.Item.UniqueId}/detail/{urlKeys.Join("/")}";

                    } else if (_type == E_Type.Edit) {
                        return $"/{_aggregate.Item.UniqueId}/edit/{urlKeys.Join("/")}";
                    } else {
                        throw new InvalidOperationException($"SingleViewの種類が不正: {_aggregate.Item}");
                    }
                }
            }
        }
        string IReactPage.DirNameInPageDir => _aggregate.Item.DisplayName.ToFileNameSafe();
        string IReactPage.ComponentPhysicalName => _type switch {
            E_Type.Create => $"{_aggregate.Item.DisplayName.ToCSharpSafe()}CreateView",
            E_Type.View => $"{_aggregate.Item.DisplayName.ToCSharpSafe()}DetailView",
            E_Type.Edit => $"{_aggregate.Item.DisplayName.ToCSharpSafe()}EditView",
            _ => throw new NotImplementedException(),
        };
        bool IReactPage.ShowMenu => false;
        string? IReactPage.LabelInMenu => null;
        SourceFile IReactPage.GetSourceFile() => Render();

        internal string FileName => _type switch {
            E_Type.Create => "new.tsx",
            E_Type.View => "detail.tsx",
            E_Type.Edit => "edit.tsx",
            _ => throw new NotImplementedException(),
        };

        internal string GetUrlStringForReact(IEnumerable<string>? keyVariables = null) {
            return GetUrlStringForReact(_type, keyVariables);
        }
        private string GetUrlStringForReact(E_Type type, IEnumerable<string>? keyVariables = null) {
            var command = type switch {
                E_Type.Create => "new",
                E_Type.View => "detail",
                E_Type.Edit => "edit",
                _ => throw new NotImplementedException(),
            };
            var queryParameter = keyVariables?
                .Select(key => $"/${{window.encodeURI(`${{{key}}}`)}}")
                ?? Enumerable.Empty<string>();

            return $"/{_aggregate.Item.UniqueId}/{command}{string.Concat(queryParameter)}";
        }

        internal SourceFile Render() => new SourceFile {
            FileName = FileName,
            RenderContent = context => {
                var controller = new Controller(_aggregate.Item);
                var multiViewUrl = new MultiViewEditable(_aggregate).Url;
                var createEmptyObject = new TSInitializerFunction(_aggregate).FunctionName;

                var find = new FindFeature(_aggregate);

                var keyName = new RefTargetKeyName(_aggregate);
                var keys = _aggregate
                    .GetKeys()
                    .OfType<AggregateMember.ValueMember>()
                    .ToArray();
                var urlKeys = _type == E_Type.Create
                    ? new[] { KEY0 }
                    : keys.Select(m => $"urlKey{m.MemberName}").ToArray();

                var names = _aggregate
                    .GetNames()
                    .OfType<AggregateMember.ValueMember>()
                    .Select(vm => vm.Declared.GetFullPath().Join("?."))
                    .ToArray();

                var maxIndent = _aggregate
                    .EnumerateDescendants()
                    .Select(a => a.EnumerateAncestors().Count())
                    .DefaultIfEmpty()
                    .Max();

                // -----------------------------------------
                // 左列の横幅の計算
                const decimal INDENT_WIDTH = 1.5m;
                var headersWidthRem = _aggregate
                    .EnumerateThisAndDescendants()
                    .SelectMany(
                        a => new AggregateDetail(a)
                            .GetOwnMembers()
                            .Where(m => {
                                // 同じ行に値を表示せず、名前が長くても行の横幅いっぱい占有できるため、除外
                                if (m is AggregateMember.Child) return false;
                                if (m is AggregateMember.Children) return false;
                                if (m is AggregateMember.Variation) return false;

                                // 画面上にメンバー名が表示されないため除外
                                if (m is AggregateMember.VariationItem) return false;
                                if (m is AggregateMember.ValueMember vm && vm.Options.InvisibleInGui) return false;

                                return true;
                            }),
                        (a, m) => new {
                            m.MemberName,
                            IndentWidth = a.EnumerateAncestors().Count() * INDENT_WIDTH, // インデント1個の幅をだいたい1.5remとして計算
                            NameWidthRem = m.MemberName.CalculateCharacterWidth() / 2 * 1.2m, // tailwindの1.2remがだいたい全角文字1文字分
                        });
                // インデント込みで最も横幅が長いメンバーの横幅を計算
                var longestHeaderWidthRem = headersWidthRem
                    .Select(x => Math.Ceiling((x.IndentWidth + x.NameWidthRem) * 10m) / 10m)
                    .DefaultIfEmpty()
                    .Max();
                // - longestHeaderWidthRemにはインデントの横幅も含まれているのでインデントの横幅を引く
                // - ヘッダ列の横幅にちょっと余裕をもたせるために+1
                var indentWidth = maxIndent * INDENT_WIDTH;
                var headerWidth = Math.Max(indentWidth, longestHeaderWidthRem - indentWidth) + 1m;

                // -----------------------------------------
                // 集約コンポーネントの宣言
                var rootAggregateList = new List<GraphNode<Aggregate>> { _aggregate };
                rootAggregateList.AddRange(_aggregate
                    .GetReferedEdgesAsSingleKeyRecursively()
                    .Select(edge => edge.Initial));

                var aggregateComponents = new List<AggregateComponent>();
                aggregateComponents.AddRange(rootAggregateList
                    .Select(agg => new AggregateComponent(agg, _type)));
                aggregateComponents.AddRange(rootAggregateList
                    .SelectMany(agg => agg.EnumerateThisAndDescendants())
                    .SelectMany(desc => desc.GetMembers())
                    .OfType<AggregateMember.RelationMember>()
                    // RefやParentを除外する
                    .Where(member => member is AggregateMember.Child
                                  || member is AggregateMember.Children
                                  || member is AggregateMember.VariationItem)
                    .Select(member => new AggregateComponent(member, _type)));

                return $$"""
                    import React, { useState, useEffect, useCallback, useMemo, useReducer, useRef, useId } from 'react';
                    import { Link, useParams, useNavigate } from 'react-router-dom';
                    import { SubmitHandler, useForm, FormProvider, useFormContext, useFieldArray } from 'react-hook-form';
                    import { BookmarkSquareIcon, PencilIcon, XMarkIcon, PlusIcon } from '@heroicons/react/24/outline';
                    import { UUID } from 'uuidjs';
                    import dayjs from 'dayjs';
                    import * as Input from '../../input';
                    import * as Layout from '../../collection';
                    import * as Util from '../../util';
                    import * as AggregateType from '../../autogenerated-types'

                    const VForm = Layout.VerticalForm

                    export default function () {
                      const [, dispatchMsg] = Util.useMsgContext()
                      const { get } = Util.useHttpRequest()
                      const { {{urlKeys.Select((urlkey, i) => $"key{i}: {urlkey}").Join(", ")}} } = useParams()
                      const [fetched, setFetched] = useState<AggregateType.{{_aggregate.Item.TypeScriptTypeName}}>()
                      const [localReposItemKey, setLocalReposItemKey] = useState<Util.ItemKey>()
                    {{If(_aggregate.Item.Options.DisableLocalRepository != true, () => $$"""
                      const localReposSettings: Util.LocalRepositoryArgs<AggregateType.{{_aggregate.Item.TypeScriptTypeName}}> = useMemo(() => ({
                        dataTypeKey: '{{LocalRepository.GetDataTypeKey(_aggregate)}}',
                        getItemKey: x => JSON.stringify([{{keys.Select(k => $"x.{k.Declared.GetFullPath().Join("?.")}").Join(", ")}}]),
                        getItemName: x => `{{names.Select(n => $"${{x.{n}}}").Join(string.Empty)}}`,
                        remoteItems: fetched ? [fetched] : undefined,
                      }), [fetched])
                      const { ready: localReposIsReady, findLocalItem } = Util.useLocalRepository(localReposSettings)
                    """)}}

                    {{If(_type == E_Type.Create, () => $$"""
                      useEffect(() => {
                        if ({{urlKeys[0]}} == null) {
                          setFetched(AggregateType.{{createEmptyObject}}())
                          setLocalReposItemKey(undefined)

                        } else if (localReposIsReady) {
                          findLocalItem({{urlKeys[0]}}).then(localReposItem => {
                            if (!localReposItem) return
                            const item = { ...localReposItem.item }

                            Util.visitObject(item, obj => {
                              // 新規データのみ主キーを編集可能にするため、読込データと新規データを区別するためのフラグをつける
                              (obj as { {{AggregateDetail.IS_LOADED}}?: boolean }).{{AggregateDetail.IS_LOADED}} = true;
                              // 配列中のオブジェクト識別用
                              (obj as { {{AggregateDetail.OBJECT_ID}}: string }).{{AggregateDetail.OBJECT_ID}} = UUID.generate()
                            })

                            setFetched({ ...item })
                            setLocalReposItemKey({{urlKeys[0]}} as Util.ItemKey)
                          })
                        }
                      }, [localReposIsReady, {{urlKeys[0]}}, findLocalItem, setFetched, setLocalReposItemKey])

                    """).Else(() => $$"""
                      useEffect(() => {
                        if (!localReposIsReady) return;
                    {{urlKeys.SelectTextTemplate(urlkey => $$"""
                        if ({{urlkey}} == null) return;
                    """)}}
                        (async () => {
                          const response = await get({{find.GetUrlStringForReact(urlKeys)}})
                          if (!response.ok) {
                            dispatchMsg(msg => msg.warn('データの読み込みに失敗しました。'))
                            return
                          }
                          const remoteReposItem = response.data as AggregateType.{{_aggregate.Item.TypeScriptTypeName}}

                          // 編集中のデータがある場合はそちらを表示
                          const itemKey = JSON.stringify([{{keys.Select(k => $"remoteReposItem.{k.Declared.GetFullPath().Join("?.")}").Join(", ")}}]) as Util.ItemKey
                          const localReposItem = await findLocalItem(itemKey)
                          const item = localReposItem ? { ...localReposItem.item } : { ...remoteReposItem }

                          Util.visitObject(item, obj => {
                            // 新規データのみ主キーを編集可能にするため、読込データと新規データを区別するためのフラグをつける
                            (obj as { {{AggregateDetail.IS_LOADED}}?: boolean }).{{AggregateDetail.IS_LOADED}} = true;
                            // 配列中のオブジェクト識別用
                            (obj as { {{AggregateDetail.OBJECT_ID}}: string }).{{AggregateDetail.OBJECT_ID}} = UUID.generate()
                          })

                          setFetched({ ...item })
                          setLocalReposItemKey(itemKey)
                        })()
                      }, [localReposIsReady, {{urlKeys.Join(", ")}}, get, setFetched, setLocalReposItemKey, dispatchMsg])

                    """)}}

                      return fetched ? (
                        <AfterLoaded
                          defaultValues={fetched}
                          localReposItemKey={localReposItemKey}
                    {{urlKeys.SelectTextTemplate(urlkey => $$"""
                          {{urlkey}}={{{urlkey}}}
                    """)}}
                        ></AfterLoaded>
                      ) : (
                        <></>
                      )
                    }

                    const AfterLoaded = ({ localReposItemKey, defaultValues{{string.Concat(urlKeys.Select(urlkey => $", {urlkey}"))}} }: {
                      localReposItemKey?: Util.ItemKey
                      defaultValues: AggregateType.{{_aggregate.Item.TypeScriptTypeName}}
                    {{urlKeys.SelectTextTemplate((urlkey, i) => $$"""
                      {{urlkey}}?: string
                    """)}}
                    }) => {
                      const navigate = useNavigate()
                      const [, dispatchMsg] = Util.useMsgContext()
                      const { get, post } = Util.useHttpRequest()
                      const reactHookFormMethods = useForm({ defaultValues })
                      const { handleSubmit } = reactHookFormMethods

                      const instanceName = useMemo(() => {
                        return `{{names.Select(n => $"${{defaultValues.{n}}}").Join(string.Empty)}}`
                      }, [defaultValues])
                    {{If(_aggregate.Item.Options.DisableLocalRepository != true, () => $$"""
                      const localReposSettings: Util.LocalRepositoryArgs<AggregateType.{{_aggregate.Item.TypeScriptTypeName}}> = useMemo(() => ({
                        dataTypeKey: '{{_aggregate.Item.ClassName}}',
                        getItemKey: x => JSON.stringify([{{keys.Select(k => $"x.{k.Declared.GetFullPath().Join("?.")}").Join(", ")}}]),
                        getItemName: x => `{{names.Select(n => $"${{x.{n}}}").Join(string.Empty)}}`,
                        remoteItems: [defaultValues],
                      }), [defaultValues])
                      const {
                        addToLocalRepository,
                        updateLocalRepositoryItem,
                      } = Util.useLocalRepository(localReposSettings)
                    """)}}

                      const formRef = useRef<HTMLFormElement>(null)
                      const onKeyDown: React.KeyboardEventHandler<HTMLFormElement> = useCallback(e => {
                        if (e.key === 'Enter' && (e.metaKey || e.ctrlKey)) {
                          // Ctrl + Enter で送信
                          formRef.current?.dispatchEvent(new Event('submit', { cancelable: true, bubbles: true }))

                        } else if (e.key === 'Enter' && !(e.target as HTMLElement).matches('textarea')) {
                          // フォーム中でEnterキーが押されたときに誤submitされるのを防ぐ。
                          // textareaでpreventDefaultすると改行できなくなるので除外
                          e.preventDefault()
                        }
                      }, [])

                    {{If(_type == E_Type.View, () => $$"""
                      const navigateToEditView = useCallback((e: React.MouseEvent) => {
                        navigate(`{{GetUrlStringForReact(E_Type.Edit, urlKeys)}}`)
                        e.preventDefault()
                      }, [navigate, {{urlKeys.Join(", ")}}])

                    """).ElseIf(_type == E_Type.Create && _aggregate.Item.Options.DisableLocalRepository == true, () => $$"""
                      const onSave: SubmitHandler<AggregateType.{{_aggregate.Item.TypeScriptTypeName}}> = useCallback(async data => {
                        const response = await post<AggregateType.{{_aggregate.Item.TypeScriptTypeName}}>(`{{controller.CreateCommandApi}}`, data)
                        if (response.ok) {
                          dispatchMsg(msg => msg.info(`${({{names.Select(path => $"String(response.data.{path})").Join(" + ")}})}を作成しました。`))
                          navigate(`{{GetUrlStringForReact(E_Type.View, keys.Select(m => AggregateDetail.GetPathOf("response.data", _aggregate, m).Join("?.")))}}`)
                        }
                      }, [post, navigate])

                    """).ElseIf(_type == E_Type.Edit && _aggregate.Item.Options.DisableLocalRepository == true, () => $$"""
                      const onSave: SubmitHandler<AggregateType.{{_aggregate.Item.TypeScriptTypeName}}> = useCallback(async data => {
                        const response = await post<AggregateType.{{_aggregate.Item.TypeScriptTypeName}}>(`{{controller.UpdateCommandApi}}`, data)
                        if (response.ok) {
                          dispatchMsg(msg => msg.info(`${({{names.Select(path => $"String(response.data.{path})").Join(" + ")}})}を更新しました。`))
                          navigate(`{{GetUrlStringForReact(E_Type.View, urlKeys)}}`)
                        }
                      }, [post, navigate, {{urlKeys.Join(", ")}}])

                    """).ElseIf(_type == E_Type.Create, () => $$"""
                      const onSave: SubmitHandler<AggregateType.{{_aggregate.Item.TypeScriptTypeName}}> = useCallback(async data => {
                        if (localReposItemKey === undefined) {
                          const { itemKey } = await addToLocalRepository(data)
                          navigate(`{{GetUrlStringForReact(E_Type.Create, new[] { "itemKey" })}}`)
                        } else {
                          await updateLocalRepositoryItem(localReposItemKey, data)
                        }
                      }, [localReposItemKey, addToLocalRepository, updateLocalRepositoryItem, navigate])

                    """).ElseIf(_type == E_Type.Edit, () => $$"""
                      const onSave: SubmitHandler<AggregateType.{{_aggregate.Item.TypeScriptTypeName}}> = useCallback(async data => {
                        if (localReposItemKey === undefined) return
                    {{urlKeys.SelectTextTemplate(urlkey => $$"""
                        if ({{urlkey}} == null) return
                    """)}}
                        await updateLocalRepositoryItem(localReposItemKey, data)
                      }, [localReposItemKey, {{urlKeys.Join(", ")}}, updateLocalRepositoryItem])

                    """)}}

                      return (
                        <FormProvider {...reactHookFormMethods}>
                    {{If(_type == E_Type.Create || _type == E_Type.Edit, () => $$"""
                          <form className="page-content-root" ref={formRef} onSubmit={handleSubmit(onSave)} onKeyDown={onKeyDown}>
                    """).Else(() => $$"""
                          <form className="page-content-root">
                    """)}}
                            <h1 className="flex text-base font-semibold select-none py-1">
                              <Link to="{{multiViewUrl}}">{{_aggregate.Item.DisplayName}}</Link>
                              &nbsp;&#047;&nbsp;
                    {{If(_type == E_Type.Create, () => $$"""
                              新規作成
                    """).Else(() => $$"""
                              <span className="select-all">{instanceName}</span>
                    """)}}
                              <div className="flex-1"></div>
                            </h1>

                            <VForm.Root leftColumnWidth="{{headerWidth}}rem">
                              {{new AggregateComponent(_aggregate, _type).RenderCaller()}}
                            </VForm.Root>

                    {{If(_type != E_Type.View && _aggregate.Item.Options.DisableLocalRepository != true, () => $$"""
                            <Input.IconButton fill className="self-start" icon={BookmarkSquareIcon}>一時保存</Input.IconButton>
                    """).ElseIf(_type == E_Type.Create, () => $$"""
                            <Input.IconButton fill className="self-start" icon={BookmarkSquareIcon}>保存</Input.IconButton>
                    """).ElseIf(_type == E_Type.View, () => $$"""
                            <Input.IconButton fill className="self-start" icon={PencilIcon} onClick={navigateToEditView}>編集</Input.IconButton>
                    """).ElseIf(_type == E_Type.Edit, () => $$"""
                            <Input.IconButton fill className="self-start" icon={BookmarkSquareIcon}>更新</Input.IconButton>
                    """)}}
                          </form>
                        </FormProvider>
                      )
                    }

                    {{aggregateComponents.SelectTextTemplate(component => component.RenderDeclaration())}}
                    """;
            },
        };
    }
}
