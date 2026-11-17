export function shortcutAction(event, focused, modalOpen = false) {
  if (
    modalOpen ||
    event.repeat ||
    focused?.isContentEditable ||
    ["INPUT", "TEXTAREA", "SELECT"].includes(focused?.tagName)
  )
    return null;
  if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "z")
    return "undo";
  if (event.code === "Space") return "pause";
  if (event.code === "Tab") return "camera";
  if (["1", "2", "3"].includes(event.key))
    return ["inspire", "focus", "fortify"][Number(event.key) - 1];
  return event.key.toLowerCase() === "r" ? "reset" : null;
}
