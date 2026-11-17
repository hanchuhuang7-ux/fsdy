export const $ = (selector) => document.querySelector(selector);
export const storageKey = "frontline-lab-v1";
export const clone = (value) => structuredClone(value);
export const escape = (s) =>
  String(s).replace(
    /[&<>"']/g,
    (c) =>
      ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[
        c
      ],
  );
