import type { LocationQuery, LocationQueryRaw, LocationQueryValue } from 'vue-router';

/** A query parameter as a string: the first one if it is given more than once, `undefined` if it is missing. */
export function queryString(
  value: LocationQueryValue | LocationQueryValue[] | undefined,
): string | undefined {
  const first = Array.isArray(value) ? value[0] : value;
  return first ?? undefined;
}

/** A query parameter's values: all of them if it is given more than once, none if it is missing. */
export function queryStrings(
  value: LocationQueryValue | LocationQueryValue[] | undefined,
): string[] {
  const values = Array.isArray(value) ? value : [value];
  return values.filter((single): single is string => typeof single === 'string');
}

/**
 * The current query with some parameters changed: those with a value are set, those without
 * (`undefined`, `null` or an empty array) are removed, all others stay. An array sets a
 * parameter that is repeated.
 */
export function mergeQuery(
  current: LocationQuery,
  changes: Record<string, string | number | (string | number)[] | null | undefined>,
): LocationQueryRaw {
  const merged: LocationQueryRaw = { ...current };
  for (const [key, value] of Object.entries(changes)) {
    if (value === undefined || value === null || (Array.isArray(value) && value.length === 0)) {
      delete merged[key];
    } else {
      merged[key] = Array.isArray(value) ? value.map(String) : String(value);
    }
  }
  return merged;
}
