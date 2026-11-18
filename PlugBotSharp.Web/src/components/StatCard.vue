<script setup>
defineProps({
  label: { type: String, required: true },
  value: { type: [String, Number], default: '-' }
})
</script>

<template>
  <div class="card">
    <div class="label">{{ label }}</div>
    <div class="value">{{ value }}</div>
  </div>
</template>
