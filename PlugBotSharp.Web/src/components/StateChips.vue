<script setup>
import { computed } from 'vue'

const props = defineProps({
  states: { type: Object, default: () => ({}) }
})

/**
 * 服务端返回的状态转成列表，name 是键，值是 { label, description, kind, value }
 */
const list = computed(() =>
  Object.entries(props.states || {}).map(([name, item]) => ({
    name,
    label: item.label || name,
    description: item.description || '',
    kind: item.kind,
    value: item.value
  }))
)

/**
 * 键值状态的展开内容
 * @param {object} item 状态
 */
function rows(item) {
  return Object.entries(item.value || {}).map(([key, value]) => ({ key, value }))
}

/**
 * 单值状态的显示文本，还没上报时用短横线顶上
 * @param {object} item 状态
 */
function valueText(item) {
  return item.value === null || item.value === undefined ? '-' : String(item.value)
}
</script>

<template>
  <div v-if="list.length" class="deps">
    <template v-for="item in list" :key="item.name">
      <details
        v-if="item.kind === 'kv'"
        class="dep state-dep state-kv"
        :title="item.description || item.name"
      >
        <summary>{{ item.label }}: {{ rows(item).length }} 项</summary>
        <div class="kv-table">
          <div v-for="row in rows(item)" :key="row.key" class="kv-line">
            <span class="mono kv-key" :title="row.key">{{ row.key }}</span>
            <span class="mono kv-value">{{ row.value }}</span>
          </div>
          <p v-if="rows(item).length === 0" class="meta">空</p>
        </div>
      </details>
      <span v-else class="dep state-dep" :title="item.description || item.name">
        {{ item.label }}: {{ valueText(item) }}
      </span>
    </template>
  </div>
</template>
