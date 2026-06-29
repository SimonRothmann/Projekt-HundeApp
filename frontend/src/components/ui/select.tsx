"use client"

import * as React from "react"
import { Select as SelectPrimitive } from "@base-ui/react/select"
import { CheckIcon, ChevronDownIcon } from "lucide-react"

import { cn } from "@/lib/utils"

// Base UI rendert in <Select.Value> standardmäßig den rohen value (bei uns
// fast immer eine DB-Id) statt eines Labels - das Auflösen passiert nur,
// wenn dem Root explizit eine "items"-Liste {value, label} übergeben wird
// (siehe base-ui Doku zu Select.Root "items"). Statt das an jeder der ca.
// ein Dutzend Verwendungsstellen im Code manuell zu pflegen, werden die
// Items hier einmalig automatisch aus den als Kindern übergebenen
// <SelectItem value=...>Label</SelectItem>-Elementen extrahiert.
function flattenToText(node: React.ReactNode): string {
  if (node == null || typeof node === "boolean") return ""
  if (typeof node === "string" || typeof node === "number") return String(node)
  if (Array.isArray(node)) return node.map(flattenToText).join("")
  if (React.isValidElement(node)) {
    return flattenToText((node.props as { children?: React.ReactNode }).children)
  }
  return ""
}

function collectSelectItems(node: React.ReactNode): Array<{ value: unknown; label: string }> {
  const items: Array<{ value: unknown; label: string }> = []
  React.Children.forEach(node, (child) => {
    if (!React.isValidElement(child)) return
    if (child.type === SelectItem) {
      const itemProps = child.props as SelectPrimitive.Item.Props
      items.push({ value: itemProps.value, label: flattenToText(itemProps.children) })
      return
    }
    const childChildren = (child.props as { children?: React.ReactNode } | undefined)?.children
    if (childChildren != null) items.push(...collectSelectItems(childChildren))
  })
  return items
}

function Select<Value>({ children, items, ...props }: SelectPrimitive.Root.Props<Value>) {
  const resolvedItems = items ?? (collectSelectItems(children) as never)
  return (
    <SelectPrimitive.Root data-slot="select" items={resolvedItems} {...props}>
      {children}
    </SelectPrimitive.Root>
  )
}

function SelectValue({ ...props }: SelectPrimitive.Value.Props) {
  return <SelectPrimitive.Value data-slot="select-value" {...props} />
}

function SelectTrigger({
  className,
  children,
  ...props
}: SelectPrimitive.Trigger.Props) {
  return (
    <SelectPrimitive.Trigger
      data-slot="select-trigger"
      className={cn(
        "flex h-9 w-full items-center justify-between gap-2 rounded-lg border border-input bg-background px-3 text-sm shadow-[var(--shadow-xs)] outline-none transition-colors hover:bg-muted/50 focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50 disabled:pointer-events-none disabled:opacity-50 data-[placeholder]:text-muted-foreground dark:bg-input/30 dark:hover:bg-input/50",
        className
      )}
      {...props}
    >
      {children}
      <SelectPrimitive.Icon data-slot="select-icon">
        <ChevronDownIcon className="size-4 shrink-0 opacity-50" />
      </SelectPrimitive.Icon>
    </SelectPrimitive.Trigger>
  )
}

function SelectPortal({ ...props }: SelectPrimitive.Portal.Props) {
  return <SelectPrimitive.Portal data-slot="select-portal" {...props} />
}

function SelectPositioner({
  className,
  sideOffset = 4,
  ...props
}: SelectPrimitive.Positioner.Props) {
  return (
    <SelectPrimitive.Positioner
      data-slot="select-positioner"
      sideOffset={sideOffset}
      className={cn("z-50 outline-none", className)}
      {...props}
    />
  )
}

function SelectContent({
  className,
  children,
  ...props
}: SelectPrimitive.Popup.Props) {
  return (
    <SelectPortal>
      <SelectPositioner>
        <SelectPrimitive.Popup
          data-slot="select-content"
          className={cn(
            "max-h-(--available-height) min-w-[var(--anchor-width)] origin-(--transform-origin) overflow-y-auto rounded-lg border border-border bg-popover bg-clip-padding p-1 text-popover-foreground shadow-lg transition duration-150 ease-in-out data-ending-style:scale-95 data-ending-style:opacity-0 data-starting-style:scale-95 data-starting-style:opacity-0",
            className
          )}
          {...props}
        >
          <SelectPrimitive.List>{children}</SelectPrimitive.List>
        </SelectPrimitive.Popup>
      </SelectPositioner>
    </SelectPortal>
  )
}

function SelectGroup({ ...props }: SelectPrimitive.Group.Props) {
  return <SelectPrimitive.Group data-slot="select-group" {...props} />
}

function SelectGroupLabel({
  className,
  ...props
}: SelectPrimitive.GroupLabel.Props) {
  return (
    <SelectPrimitive.GroupLabel
      data-slot="select-group-label"
      className={cn(
        "px-2 py-1.5 text-xs font-medium text-muted-foreground",
        className
      )}
      {...props}
    />
  )
}

function SelectItem({
  className,
  children,
  ...props
}: SelectPrimitive.Item.Props) {
  return (
    <SelectPrimitive.Item
      data-slot="select-item"
      className={cn(
        "relative flex w-full cursor-default items-center gap-2 rounded-md py-1.5 pr-7 pl-2 text-sm outline-none select-none data-disabled:pointer-events-none data-disabled:opacity-50 data-highlighted:bg-muted data-highlighted:text-foreground",
        className
      )}
      {...props}
    >
      <SelectPrimitive.ItemText>{children}</SelectPrimitive.ItemText>
      <SelectPrimitive.ItemIndicator className="absolute right-2 flex items-center">
        <CheckIcon className="size-4" />
      </SelectPrimitive.ItemIndicator>
    </SelectPrimitive.Item>
  )
}

export {
  Select,
  SelectValue,
  SelectTrigger,
  SelectContent,
  SelectGroup,
  SelectGroupLabel,
  SelectItem,
}
