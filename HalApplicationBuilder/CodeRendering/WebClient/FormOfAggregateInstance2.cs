using HalApplicationBuilder.Core;
using HalApplicationBuilder.DotnetEx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static HalApplicationBuilder.CodeRendering.TemplateTextHelper;

namespace HalApplicationBuilder.CodeRendering.WebClient {
    partial class FormOfAggregateInstance {
        protected override string Template() {
            var components = _instance
                .EnumerateThisAndDescendants()
                .Select(x => new Component(x));

            return $$"""
                import React, { useCallback } from 'react'
                import { PlusIcon, XMarkIcon } from '@heroicons/react/24/outline'
                import { useForm, useFieldArray, useFormContext } from 'react-hook-form'
                import { usePageContext } from '../../hooks/PageContext'
                import * as Components from '../../components'
                import * as AggregateType from '{{TypesImport}}'

                {{components.SelectTextTemplate(RenderComponent)}}
                """;
        }

        private string RenderComponent(Component component) {
            if (!component.IsChildren) {
                var args = GetArguments(component.AggregateInstance).Values;
                var layout = component.AggregateInstance.GetProperties(_ctx.Config).SelectTextTemplate(p => RenderProperty(component, p));

                return $$"""
                    export const {{component.ComponentName}} = ({ {{args.Join(", ")}} }: {
                    {{args.SelectTextTemplate(arg => $$"""
                      {{arg}}: number
                    """)}}
                    }) => {
                      const [{ pageIsReadOnly },] = usePageContext()
                      const { register, watch } = useFormContext<AggregateType.{{_instance.Item.TypeScriptTypeName}}>()

                      return (
                        <>
                          {{WithIndent(layout, "      ")}}
                        </>
                      )
                    }
                    """;

            } else {
                var argsAndIndex = GetArguments(component.AggregateInstance).Values;
                var args = argsAndIndex.SkipLast(1);
                var index = argsAndIndex.Last();
                var layout = component.AggregateInstance.GetProperties(_ctx.Config).SelectTextTemplate(p => RenderProperty(component, p));
                var createNewChildrenItem = new types.AggregateInstanceInitializerFunction(component.AggregateInstance).FunctionName;

                return $$"""
                    export const {{component.ComponentName}} = ({ {{args.Join(", ")}} }: {
                    {{args.SelectTextTemplate(arg => $$"""
                      {{arg}}: number
                    """)}}
                    }) => {
                      const [{ pageIsReadOnly },] = usePageContext()
                      const { register, watch, control } = useFormContext<AggregateType.{{_instance.Item.TypeScriptTypeName}}>()
                      const { fields, append, remove } = useFieldArray({
                        control,
                        name: `{{component.GetUseFieldArrayName()}}`,
                      })
                      const onAdd = useCallback((e: React.MouseEvent) => {
                        append(AggregateType.{{createNewChildrenItem}}())
                        e.preventDefault()
                      }, [append])
                      const onRemove = useCallback((index: number) => {
                        return (e: React.MouseEvent) => {
                          remove(index)
                          e.preventDefault()
                        }
                      }, [remove])

                      return (
                        <>
                          {fields.map((_, {{index}}) => (
                            <div key={{{index}}} className="flex flex-col space-y-1 p-1 border border-neutral-400">
                              {{WithIndent(layout, "          ")}}
                              {!pageIsReadOnly &&
                                <Components.IconButton
                                  underline
                                  icon={XMarkIcon}
                                  onClick={onRemove({{index}})}
                                  className="self-start">
                                  削除
                                </Components.IconButton>}
                            </div>
                          ))}
                          {!pageIsReadOnly &&
                            <Components.IconButton
                              underline
                              icon={PlusIcon}
                              onClick={onAdd}
                              className="self-start">
                              追加
                            </Components.IconButton>}
                        </>
                      )
                    }
                    """;
            }
        }

        private string RenderProperty(Component component, AggregateInstance.Property prop) {
            if (prop is AggregateInstance.SchalarProperty schalar) {
                return $$"""
                    <div className="flex">
                      <div className="{{PropNameWidth}}">
                      <span className="text-sm select-none opacity-80">
                        {{prop.PropertyName}}
                      </span>
                      </div>
                      <div className="flex-1">
                        {{RenderSchalarProperty(component.AggregateInstance, schalar, "    ")}}
                      </div>
                    </div>
                    """;

            } else if (prop is AggregateInstance.RefProperty refProperty) {
                var combobox = new ComboBox(refProperty.RefTarget.GetCorrespondingAggregate(), _ctx);
                var registerName = GetRegisterName(component.AggregateInstance, refProperty).Value;
                return $$"""
                    <div className="flex">
                      <div className="{{PropNameWidth}}">
                      <span className="text-sm select-none opacity-80">
                        {{prop.PropertyName}}
                      </span>
                      </div>
                      <div className="flex-1">
                        {{WithIndent(combobox.RenderCaller(registerName), "    ")}}
                      </div>
                    </div>
                    """;

            } else if (prop is AggregateInstance.ChildProperty child) {
                var childComponent = new Component(child.ChildAggregateInstance);
                return $$"""
                    <div className="py-2">
                      <span className="text-sm select-none opacity-80">
                      {{prop.PropertyName}}
                      </span>
                      <div className="flex flex-col space-y-1 p-1 border border-neutral-400">
                        {{WithIndent(childComponent.RenderCaller(), "    ")}}
                      </div>
                    </div>
                    """;

            } else if (prop is AggregateInstance.VariationProperty variation) {
                var childComponent = new Component(variation.ChildAggregateInstance);
                var switchProp = GetRegisterName(component.AggregateInstance, variation.Group).Value;
                return $$"""
                    <div className={`flex flex-col space-y-1 p-1 border border-neutral-400 ${(watch(`{{switchProp}}`) !== '{{variation.Key}}' ? 'hidden' : '')}`}>
                      {{WithIndent(childComponent.RenderCaller(), "  ")}}
                    </div>
                    """;

            } else if (prop is AggregateInstance.VariationSwitchProperty variationSwitch) {
                var switchProp = GetRegisterName(component.AggregateInstance, variationSwitch).Value;
                return $$"""
                    <div className="flex">
                      <div className="{{PropNameWidth}}">
                      <span className="text-sm select-none opacity-80">
                        {{variationSwitch.CorrespondingDbColumn.PropertyName}}
                      </span>
                      </div>
                      <div className="flex-1 flex gap-2 flex-wrap">
                    {{variationSwitch.CorrespondingDbColumn.Group.VariationAggregates.SelectTextTemplate(item => $$"""
                        <label>
                          <input type="radio" value="{{item.Key}}" disabled={pageIsReadOnly} {...register(`{{switchProp}}`)} />
                          {{item.Value.RelationName}}
                        </label>
                    """)}}
                      </div>
                    </div>
                    """;

            } else if (prop is AggregateInstance.ChildrenProperty children) {
                var childrenComponent = new Component(children.ChildAggregateInstance);
                return $$"""
                    <div className="py-2">
                      <span className="text-sm select-none opacity-80">
                        {{prop.PropertyName}}
                      </span>
                      <div className="flex flex-col space-y-1">
                        {{WithIndent(childrenComponent.RenderCaller(), "    ")}}
                      </div>
                    </div>
                    """;

            } else {
                throw new NotImplementedException();
            }
        }
    }
}