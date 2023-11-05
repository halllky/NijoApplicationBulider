import React, { useRef } from "react"
import { Link } from "react-router-dom"
import { CircleStackIcon, Cog8ToothIcon, PlayCircleIcon } from "@heroicons/react/24/outline"
import { menuItems, THIS_APPLICATION_NAME } from ".."
import { Panel, PanelGroup, PanelResizeHandle } from "react-resizable-panels"
import { NavLink, useLocation } from "react-router-dom"
import { LOCAL_STORAGE_KEYS } from "./localStorageKeys"

export const SideMenu = () => {
  return (
    <PanelGroup direction="vertical" className="bg-color-gutter" autoSaveId={LOCAL_STORAGE_KEYS.SIDEBAR_SIZE_Y}>

      <Panel className="flex flex-col">
        <Link to='/' className="p-1 ellipsis-ex font-semibold select-none">
          {THIS_APPLICATION_NAME}
        </Link>
        <nav className="flex-1 overflow-y-auto leading-none">
          {menuItems.map(item =>
            <SideMenuLink key={item.url} url={item.url}>{item.text}</SideMenuLink>
          )}
        </nav>
      </Panel>

      <PanelResizeHandle className="h-1 border-b border-color-5" />

      <Panel className="flex flex-col">
        <nav className="flex-1 overflow-y-auto leading-none">
          <SideMenuLink url="/bagkground-tasks" icon={PlayCircleIcon}>バッチ処理</SideMenuLink>
          <SideMenuLink url="/settings" icon={Cog8ToothIcon}>設定</SideMenuLink>
        </nav>
        <span className="p-1 text-sm whitespace-nowrap overflow-hidden">
          ver. 0.9.0.0
        </span>
      </Panel>
    </PanelGroup>
  )
}

const SideMenuLink = ({ url, icon, children }: {
  url: string
  icon?: React.ElementType
  children?: React.ReactNode
}) => {

  const location = useLocation()
  const className = location.pathname.startsWith(url)
    ? 'outline-none inline-block w-full p-1 ellipsis-ex font-bold bg-color-base'
    : 'outline-none inline-block w-full p-1 ellipsis-ex'

  return (
    <NavLink to={url} className={className}>
      {React.createElement(icon ?? CircleStackIcon, { className: 'inline w-4 mr-1 opacity-70 align-middle' })}
      <span className="text-sm align-middle select-none">{children}</span>
    </NavLink>
  )
}