import type { LocationQuery, LocationQueryRaw, LocationQueryValue } from 'vue-router';

/** A query parameter as a string: the first one if it is given more than once, `undefined` if it is missing. */
export function queryString(
  value: LocationQueryValue | LocationQueryValue[] | undefined,
): string | undefined {
  const first = Array.isArray(value) ? value[0] : value;
  return first ?? undefined;
}

/**
 * The current query with some parameters changed: those with a value are set, those without
 * (`undefined` or `null`) are removed, all others stay.
 */
export function mergeQuery(
  current: LocationQuery,
  changes: Record<string, string | number | null | undefined>,
): LocationQueryRaw {
  const merged: LocationQueryRaw = { ...current };
  for (const [key, value] of Object.entries(changes)) {
    if (value === undefined || value === null) {
      delete merged[key];
    } else {
      merged[key] = String(value);
    }
  }
  return merged;
}
