/** Builds query parameters, leaving out empty values. An array repeats the parameter: `genreId=1&genreId=2`. */
export function toSearchParams(query: object): URLSearchParams {
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(query)) {
    for (const single of Array.isArray(value) ? value : [value]) {
      if (single !== undefined && single !== null && single !== '') {
        params.append(key, String(single));
      }
    }
  }
  return params;
}
