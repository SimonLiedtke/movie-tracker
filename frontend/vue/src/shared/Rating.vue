<script setup lang="ts">
import { computed } from 'vue';
import { formatRating } from './rating-format';

const props = defineProps<{
  /** Half stars: 1 = 0.5 stars, 10 = 5 stars. */
  halfStars?: number | null;
}>();

const stars = computed(() => formatRating(props.halfStars));
const title = computed(() => `${(props.halfStars ?? 0) / 2} of 5 stars`);
</script>

<template>
  <span v-if="halfStars" class="rating-stars" :title="title" :aria-label="title">{{ stars }}</span>
</template>
