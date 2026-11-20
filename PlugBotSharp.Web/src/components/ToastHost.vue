<script setup>
import { toasts } from '../toast'
</script>

<template>
  <div class="toasts">
    <div v-for="item in toasts" :key="item.id" class="toast" :class="item.type">
      {{ item.message }}
    </div>
  </div>
</template>
