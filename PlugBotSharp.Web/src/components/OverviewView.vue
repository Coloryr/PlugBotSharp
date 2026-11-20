<script setup>
import { computed } from 'vue'
import StatCard from './StatCard.vue'

const props = defineProps({
  overview: { type: Object, default: null }
})

const version = computed(() => props.overview?.version ?? '-')
const uptime = computed(() => props.overview?.uptime ?? '-')
const components = computed(() => props.overview?.components ?? {})
const instances = computed(() => props.overview?.instances ?? {})

const componentHint = computed(() => {
  const c = components.value
  return c.total === undefined ? '' : `已加载 ${c.loaded ?? 0} / 已启用 ${c.enabled ?? 0}`
})

const instanceHint = computed(() => {
  const i = instances.value
  return i.total === undefined ? '' : `已加载 ${i.loaded ?? 0} / 已启用 ${i.enabled ?? 0}`
})
</script>

<template>
  <div class="grid">
    <StatCard label="运行时长" :value="uptime" :hint="overview?.startTime ? '启动于 ' + overview.startTime : ''" />
    <StatCard label="组件" :value="components.total ?? '-'" :hint="componentHint" />
    <StatCard label="插件 / 代理 / 运行库" :value="`${components.plugins ?? 0} / ${components.proxies ?? 0} / ${components.libs ?? 0}`" hint="按类型统计" />
    <StatCard label="实例" :value="instances.total ?? '-'" :hint="instanceHint" />
    <StatCard label="核心版本" :value="version" hint="PlugBotSharp.Core" />
  </div>
</template>
