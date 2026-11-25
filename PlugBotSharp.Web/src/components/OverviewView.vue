<script setup>
import { computed } from 'vue'
import StatCard from './StatCard.vue'
import StateChips from './StateChips.vue'

const props = defineProps({
  overview: { type: Object, default: null }
})

const version = computed(() => props.overview?.version ?? '-')
const uptime = computed(() => props.overview?.uptime ?? '-')
const components = computed(() => props.overview?.components ?? {})
const instances = computed(() => props.overview?.instances ?? {})

const componentItems = computed(() => props.overview?.componentItems ?? [])
const instanceItems = computed(() => props.overview?.instanceItems ?? [])

/**
 * 把组件和实例上报的自定义状态按对象分组，一组是一块监控面板
 */
const stateGroups = computed(() => {
  const groups = []

  const add = (key, title, source, states) => {
    const items = Object.entries(states || {}).map(([name, state]) => ({
      name,
      label: state?.label || name,
      description: state?.description || '',
      kind: state?.kind,
      value: state?.value
    }))

    if (items.length === 0) {
      return
    }

    items.sort((a, b) => a.label.localeCompare(b.label, 'zh-Hans-CN'))
    groups.push({ key, title, source, states: items })
  }

  for (const item of componentItems.value) {
    add('c:' + item.id, item.name || item.id, '组件 ' + item.id, item.states)
  }

  for (const item of instanceItems.value) {
    add('i:' + item.uuid, item.name || shortUuid(item.uuid), '实例 ' + item.proxy, item.states)
  }

  return groups
})

/**
 * 所有对象加起来一共几项状态
 */
const stateCount = computed(() => stateGroups.value.reduce((sum, group) => sum + group.states.length, 0))

/**
 * 键值状态展开成一行行
 * @param {object} value 状态值
 */
function kvRows(value) {
  return Object.entries(value || {}).map(([key, item]) => ({ key, value: item }))
}

/**
 * 单值状态的显示文本，没上报时用短横线
 * @param {object} state 状态
 */
function stateText(state) {
  const value = state?.value

  return value === null || value === undefined ? '-' : String(value)
}

const typeName = { Plugin: '插件', Proxy: '代理', Lib: '运行库' }
const stateName = {
  enabled: '运行中',
  disabled: '已禁用',
  loaded: '已加载',
  unloaded: '未加载',
  blocked: '依赖缺失'
}

const componentHint = computed(() => {
  const c = components.value
  return c.total === undefined ? '' : `已加载 ${c.loaded ?? 0} / 已启用 ${c.enabled ?? 0}`
})

const instanceHint = computed(() => {
  const i = instances.value
  return i.total === undefined ? '' : `已加载 ${i.loaded ?? 0} / 已启用 ${i.enabled ?? 0}`
})

/**
 * 实例 uuid 太长，列表里只显示前 8 位
 * @param {string} uuid 实例 uuid
 */
function shortUuid(uuid) {
  return String(uuid || '').slice(0, 8)
}
</script>

<template>
  <div class="grid">
    <StatCard label="运行时长" :value="uptime" :hint="overview?.startTime ? '启动于 ' + overview.startTime : ''" />
    <StatCard label="组件" :value="components.total ?? '-'" :hint="componentHint" />
    <StatCard label="插件 / 代理 / 运行库" :value="`${components.plugins ?? 0} / ${components.proxies ?? 0} / ${components.libs ?? 0}`" hint="按类型统计" />
    <StatCard label="实例" :value="instances.total ?? '-'" :hint="instanceHint" />
    <StatCard label="核心版本" :value="version" hint="PlugBotSharp.Core" />
  </div>

  <section class="panels">
    <div class="panel">
      <header class="panel-head">
        <h3>组件状态</h3>
        <span class="meta">{{ componentItems.length }} 个</span>
      </header>

      <p v-if="componentItems.length === 0" class="empty">还没有组件</p>

      <div v-else class="status-list">
        <div v-for="item in componentItems" :key="item.id" class="status-row">
          <div class="status-main">
            <div class="name">{{ item.name || item.id }}</div>
            <div class="meta">
              <span class="mono">{{ item.id }}</span>
              · {{ typeName[item.type] || item.type }}<template v-if="item.version"> · {{ item.version }}</template>
            </div>
            <StateChips :states="item.states" />
          </div>

          <span class="state" :class="item.state">{{ stateName[item.state] || item.state }}</span>
        </div>
      </div>
    </div>

    <div class="panel">
      <header class="panel-head">
        <h3>实例状态</h3>
        <span class="meta">{{ instanceItems.length }} 个</span>
      </header>

      <p v-if="instanceItems.length === 0" class="empty">还没有实例</p>

      <div v-else class="status-list">
        <div v-for="item in instanceItems" :key="item.uuid" class="status-row">
          <div class="status-main">
            <div class="name">{{ item.name || '未命名' }}</div>
            <div class="meta">
              <span class="mono" :title="item.uuid">{{ shortUuid(item.uuid) }}</span>
              · {{ item.proxy }}
            </div>
            <StateChips :states="item.states" />
          </div>

          <span class="state" :class="item.state">{{ stateName[item.state] || item.state }}</span>
        </div>
      </div>
    </div>
  </section>

  <section v-if="stateGroups.length" class="panel panel-full">
    <header class="panel-head">
      <h3>自定义状态</h3>
      <span class="meta">组件和实例上报的 {{ stateCount }} 项，按对象分组</span>
    </header>

    <div v-for="group in stateGroups" :key="group.key" class="state-group">
      <div class="state-group-head">
        <span class="name">{{ group.title }}</span>
        <span class="meta">{{ group.source }}</span>
      </div>

      <div class="grid state-grid">
        <div
          v-for="state in group.states"
          :key="state.name"
          class="card state-card"
          :title="state.description || state.label"
        >
          <div class="label">{{ state.label }}</div>

          <template v-if="state.kind === 'kv'">
            <div class="value">{{ kvRows(state.value).length }} 项</div>
            <details v-if="kvRows(state.value).length" class="card-kv">
              <summary>查看</summary>
              <div class="kv-table">
                <div v-for="item in kvRows(state.value)" :key="item.key" class="kv-line">
                  <span class="mono kv-key" :title="item.key">{{ item.key }}</span>
                  <span class="mono kv-value">{{ item.value }}</span>
                </div>
              </div>
            </details>
          </template>

          <div v-else class="value">{{ stateText(state) }}</div>

          <div v-if="state.description" class="hint">{{ state.description }}</div>
        </div>
      </div>
    </div>
  </section>
</template>