<script setup lang="ts">
import { computed } from 'vue';

const props = defineProps<{
  page: number;
  pageSize: number;
  totalCount: number;
}>();

const emit = defineEmits<{
  pageChange: [page: number];
}>();

const pageCount = computed(() => Math.ceil(props.totalCount / props.pageSize));

/** First, last, and the pages around the current one; null marks a gap. */
const pages = computed(() => {
  const current = props.page;
  const last = pageCount.value;
  const result: (number | null)[] = [];
  for (let p = 1; p <= last; p++) {
    if (p === 1 || p === last || Math.abs(p - current) <= 1) {
      result.push(p);
    } else if (result.at(-1) !== null) {
      result.push(null);
    }
  }
  return result;
});
</script>

<template>
  <nav
    v-if="pageCount > 1"
    class="pagination is-centered mt-5"
    role="navigation"
    aria-label="pagination"
  >
    <button class="pagination-previous" :disabled="page <= 1" @click="emit('pageChange', page - 1)">
      Previous
    </button>
    <button
      class="pagination-next"
      :disabled="page >= pageCount"
      @click="emit('pageChange', page + 1)"
    >
      Next
    </button>
    <ul class="pagination-list">
      <li v-for="(p, index) in pages" :key="index">
        <span v-if="p === null" class="pagination-ellipsis">&hellip;</span>
        <button
          v-else
          class="pagination-link"
          :class="{ 'is-current': p === page }"
          :aria-current="p === page ? 'page' : undefined"
          @click="emit('pageChange', p)"
        >
          {{ p }}
        </button>
      </li>
    </ul>
  </nav>
</template>
