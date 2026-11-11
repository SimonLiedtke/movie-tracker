<script setup lang="ts">
import { useRouter } from 'vue-router';
import { mergeQuery } from './query';

const props = defineProps<{
  tags: readonly string[];
  /** The route of the list that the tags filter, e.g. "/diary". */
  route: string;
  /** The tag the list is filtered by now, if any. */
  active?: string;
}>();

const router = useRouter();

/** Each tag leads to the list filtered by it, from page 1. */
const link = (tag: string) => ({
  path: props.route,
  query: mergeQuery(router.currentRoute.value.query, { tag, page: undefined }),
});
</script>

<template>
  <div v-if="tags.length" class="tags mb-0">
    <RouterLink
      v-for="tag in tags"
      :key="tag"
      class="tag"
      :class="tag === active ? 'is-info' : 'is-light'"
      :to="link(tag)"
      :title="`Show everything tagged ${tag}`"
    >
      {{ tag }}
    </RouterLink>
  </div>
</template>
