<script setup>
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { api } from '../api'
import { notify } from '../toast'
import InstallDialog from './InstallDialog.vue'

const items = ref([])
const loading = ref(false)
const error = ref('')
const filter = ref('all')
const keyword = ref('')
const busy = ref('')
const installing = ref(false)

const typeName = { Plugin: '插件', Proxy: '代理', Lib: '运行库' }
const stateName = { enabled: '运行中', disabled: '已禁用', loaded: '已加载', unloaded: '未加载', blocked: '依赖缺失' }

const counts = computed(() => ({
  all: items.value.length,
  Plugin: items.value.filter(x => x.type === 'Plugin').length,
  Proxy: items.value.filter(x => x.type === 'Proxy').length,
  Lib: items.value.filter(x => x.type === 'Lib').length
}))

const filtered = computed(() => {
  const kw = keyword.value.trim().toLowerCase()

  return items.value.filter(x => {
    const byType = filter.value === 'all' || x.type === filter.value
    const byKey = !kw || x.id.toLowerCase().includes(kw) || (x.name || '').toLowerCase().includes(kw)

    return byType && byKey
  })
})

/**
 * 拉取组件列表
 */
async function refresh() {
  loading.value = true

  try {
    const res = await api.get('admin/components')
    items.value = res.data || []
    error.value = ''
  } catch (e) {
    error.value = e.message
  } finally {
    loading.value = false
  }
}

/**
 * 执行组件操作：load / unload / enable / disable
 * @param {object} item 组件
 * @param {string} action 动作
 * @param {string} label 按钮文案，用于提示
 */
async function act(item, action, label) {
  busy.value = item.id + ':' + action

  try {
    const res = await api.post(`admin/components/${encodeURIComponent(item.id)}/${action}`)
    notify(res.message || label + '成功', 'ok')
  } catch (e) {
    notify(label + '失败：' + e.message, 'bad')
  } finally {
    busy.value = ''
    await refresh()
  }
}

/**
 * 删除组件
 * @param {object} item 组件
 */
async function remove(item) {
  if (!confirm(`确定删除组件 ${item.id}？\n\n组件目录会被删除，Configs 里的配置会保留。`)) {
    return
  }

  busy.value = item.id + ':delete'

  try {
    const res = await api.del(`admin/components/${encodeURIComponent(item.id)}`)
    notify(res.message || '已删除', 'ok')
  } catch (e) {
    notify('删除失败：' + e.message, 'bad')
  } finally {
    busy.value = ''
    await refresh()
  }
}

let timer = null

onMounted(() => {
  refresh()
  timer = setInterval(refresh, 5000)
})

onBeforeUnmount(() => {
  if (timer) {
    clearInterval(timer)
  }
})
</script>

<template>
  <section class="section">
    <div class="toolbar">
      <div class="seg">
        <button :class="{ active: filter === 'all' }" @click="filter = 'all'">全部 {{ counts.all }}</button>
        <button :class="{ active: filter === 'Plugin' }" @click="filter = 'Plugin'">插件 {{ counts.Plugin }}</button>
        <button :class="{ active: filter === 'Proxy' }" @click="filter = 'Proxy'">代理 {{ counts.Proxy }}</button>
        <button :class="{ active: filter === 'Lib' }" @click="filter = 'Lib'">运行库 {{ counts.Lib }}</button>
      </div>

      <input v-model="keyword" class="search" placeholder="搜索名称或 id" />

      <span class="spacer" />

      <button class="btn" :disabled="loading" @click="refresh">刷新</button>
      <button class="btn primary" @click="installing = true">安装组件</button>
    </div>

    <p v-if="error" class="error">{{ error }}</p>

    <div v-if="filtered.length === 0" class="empty">
      {{ loading ? '加载中…' : '还没有组件，先安装一个吧' }}
    </div>

    <table v-else class="table">
      <thead>
        <tr>
          <th>组件</th>
          <th>类型</th>
          <th>版本</th>
          <th>状态</th>
          <th class="right">操作</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="item in filtered" :key="item.id">
          <td>
            <div class="name">{{ item.name || item.id }}</div>
            <div class="meta" :title="item.folder">{{ item.id }}</div>
            <div v-if="item.deps && item.deps.length" class="deps">
              <span v-for="dep in item.deps" :key="dep.type + dep.id" class="dep">
                {{ dep.type === 'Nuget' ? 'NuGet' : '组件' }} {{ dep.id }} {{ dep.version }}
              </span>
            </div>
          </td>
          <td><span class="tag">{{ typeName[item.type] || item.type }}</span></td>
          <td class="mono">{{ item.version || '-' }}</td>
          <td>
            <span class="state" :class="item.state">{{ stateName[item.state] || item.state }}</span>
          </td>
          <td class="actions">
            <button
              v-if="!item.loaded"
              class="btn"
              :disabled="!!busy"
              @click="act(item, 'load', '加载')"
            >
              加载
            </button>

            <template v-else>
              <button
                v-if="item.type !== 'Lib'"
                class="btn"
                :disabled="!!busy || item.blocked"
                @click="act(item, item.enabled ? 'disable' : 'enable', item.enabled ? '禁用' : '启用')"
              >
                {{ item.enabled ? '禁用' : '启用' }}
              </button>
              <button class="btn" :disabled="!!busy" @click="act(item, 'unload', '卸载')">卸载</button>
            </template>

            <button class="btn danger" :disabled="!!busy" @click="remove(item)">删除</button>
          </td>
        </tr>
      </tbody>
    </table>

    <InstallDialog :open="installing" @close="installing = false" @installed="refresh" />
  </section>
</template>
