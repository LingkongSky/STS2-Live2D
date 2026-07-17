import { defineConfig } from 'vitepress'

const base = process.env.DOCS_BASE ?? '/'
const editLinkPattern = ({ filePath }: { filePath: string }) =>
  `https://github.com/LingkongSky/STS2-Live2D/edit/main/docs/${filePath}`

const socialLinks = [
  { icon: 'github' as const, link: 'https://github.com/LingkongSky/STS2-Live2D' }
]

export default defineConfig({
  title: 'STS2 Live2D',
  description: 'Live2D runtime, model management, and public Mod API for Slay the Spire 2.',
  base,
  rewrites: {
    'content/zh-CN/:path*': ':path*',
    'content/en-US/:path*': 'en/:path*',
    'content/ja-JP/:path*': 'ja/:path*'
  },
  cleanUrls: true,
  lastUpdated: true,
  sitemap: {
    hostname: 'https://lingkongsky.github.io/STS2-Live2D/'
  },
  locales: {
    root: {
      label: '简体中文',
      lang: 'zh-CN',
      link: '/',
      title: 'STS2 Live2D',
      description: '为《杀戮尖塔 2》提供 Live2D 模型显示、配置与第三方 Mod 控制 API。'
    },
    en: {
      label: 'English',
      lang: 'en-US',
      link: '/en/',
      title: 'STS2 Live2D',
      description: 'Live2D model display, configuration, and public Mod API for Slay the Spire 2.'
    },
    ja: {
      label: '日本語',
      lang: 'ja-JP',
      link: '/ja/',
      title: 'STS2 Live2D',
      description: '『Slay the Spire 2』向け Live2D モデル表示・設定・Mod API。'
    }
  },
  head: [
    ['meta', { name: 'theme-color', content: '#17151f' }],
    ['meta', { property: 'og:type', content: 'website' }],
    ['meta', { property: 'og:title', content: 'STS2 Live2D' }],
    ['meta', { property: 'og:description', content: 'Live2D runtime, model management, and public Mod API.' }]
  ],
  markdown: {
    lineNumbers: true
  },
  themeConfig: {
    siteTitle: 'STS2 Live2D',
    socialLinks,
    search: {
      provider: 'local',
      options: {
        locales: {
          root: {
            translations: {
              button: { buttonText: '搜索文档', buttonAriaLabel: '搜索文档' },
              modal: {
                displayDetails: '显示详细列表',
                resetButtonTitle: '清除查询',
                backButtonTitle: '关闭搜索',
                noResultsText: '没有找到相关内容',
                footer: {
                  selectText: '选择',
                  selectKeyAriaLabel: '回车',
                  navigateText: '切换',
                  navigateUpKeyAriaLabel: '上箭头',
                  navigateDownKeyAriaLabel: '下箭头',
                  closeText: '关闭',
                  closeKeyAriaLabel: 'Esc'
                }
              }
            }
          },
          en: {
            translations: {
              button: { buttonText: 'Search', buttonAriaLabel: 'Search documentation' },
              modal: {
                displayDetails: 'Display detailed list',
                resetButtonTitle: 'Reset search',
                backButtonTitle: 'Close search',
                noResultsText: 'No results found',
                footer: {
                  selectText: 'Select',
                  selectKeyAriaLabel: 'Enter',
                  navigateText: 'Navigate',
                  navigateUpKeyAriaLabel: 'Up arrow',
                  navigateDownKeyAriaLabel: 'Down arrow',
                  closeText: 'Close',
                  closeKeyAriaLabel: 'Escape'
                }
              }
            }
          },
          ja: {
            translations: {
              button: { buttonText: '検索', buttonAriaLabel: 'ドキュメントを検索' },
              modal: {
                displayDetails: '詳細一覧を表示',
                resetButtonTitle: '検索をリセット',
                backButtonTitle: '検索を閉じる',
                noResultsText: '結果が見つかりません',
                footer: {
                  selectText: '選択',
                  selectKeyAriaLabel: 'Enter',
                  navigateText: '移動',
                  navigateUpKeyAriaLabel: '上矢印',
                  navigateDownKeyAriaLabel: '下矢印',
                  closeText: '閉じる',
                  closeKeyAriaLabel: 'Esc'
                }
              }
            }
          }
        }
      }
    },
    locales: {
      root: {
        label: '简体中文',
        nav: [
          { text: '玩家指南', link: '/guide/getting-started' },
          { text: 'Mod 接入', link: '/integration/getting-started' },
          { text: '参考', link: '/reference/api' },
          { text: '维护', link: '/maintainers/development' },
          { text: 'v0.4.1 · API 4', link: '/reference/api' }
        ],
        sidebar: {
          '/guide/': [{ text: '玩家指南', items: [
            { text: '开始使用', link: '/guide/getting-started' },
            { text: '模型管理', link: '/guide/models' },
            { text: '显示与渲染', link: '/guide/appearance' },
            { text: '动作与快捷键', link: '/guide/actions' },
            { text: '故障排查', link: '/guide/troubleshooting' }
          ] }],
          '/integration/': [
            { text: 'Mod 接入', items: [
              { text: '五分钟接入', link: '/integration/getting-started' },
              { text: '模型控制', link: '/integration/model-api' },
              { text: '线程与高频更新', link: '/integration/threading' },
              { text: '自带模型 Pack', link: '/integration/packs' }
            ] },
            { text: '参考', items: [
              { text: 'API 参考', link: '/reference/api' },
              { text: 'Pack 格式', link: '/reference/pack-format' }
            ] }
          ],
          '/reference/': [{ text: '参考', items: [
            { text: '公共 API', link: '/reference/api' },
            { text: '配置结构', link: '/reference/configuration' },
            { text: 'Pack 格式', link: '/reference/pack-format' }
          ] }],
          '/maintainers/': [{ text: '维护者', items: [
            { text: '开发与架构', link: '/maintainers/development' },
            { text: '测试与发布', link: '/maintainers/release' }
          ] }]
        },
        outline: { level: [2, 3], label: '本页内容' },
        editLink: { pattern: editLinkPattern, text: '在 GitHub 上编辑此页' },
        lastUpdated: { text: '最后更新于' },
        docFooter: { prev: '上一页', next: '下一页' },
        darkModeSwitchLabel: '主题',
        sidebarMenuLabel: '菜单',
        returnToTopLabel: '返回顶部',
        langMenuLabel: '切换语言',
        footer: { message: '当前为开发预览文档，仅描述最新实现。', copyright: 'Released under the MIT License.' }
      },
      en: {
        label: 'English',
        nav: [
          { text: 'Player Guide', link: '/en/guide/getting-started' },
          { text: 'Mod Integration', link: '/en/integration/getting-started' },
          { text: 'Reference', link: '/en/reference/api' },
          { text: 'Maintainers', link: '/en/maintainers/development' },
          { text: 'v0.4.1 · API 4', link: '/en/reference/api' }
        ],
        sidebar: {
          '/en/guide/': [{ text: 'Player Guide', items: [
            { text: 'Getting Started', link: '/en/guide/getting-started' },
            { text: 'Model Management', link: '/en/guide/models' },
            { text: 'Display and Rendering', link: '/en/guide/appearance' },
            { text: 'Actions and Hotkeys', link: '/en/guide/actions' },
            { text: 'Troubleshooting', link: '/en/guide/troubleshooting' }
          ] }],
          '/en/integration/': [
            { text: 'Mod Integration', items: [
              { text: 'Five-minute Setup', link: '/en/integration/getting-started' },
              { text: 'Model Control', link: '/en/integration/model-api' },
              { text: 'Threads and Streaming Updates', link: '/en/integration/threading' },
              { text: 'Bundled Model Packs', link: '/en/integration/packs' }
            ] },
            { text: 'Reference', items: [
              { text: 'API Reference', link: '/en/reference/api' },
              { text: 'Pack Format', link: '/en/reference/pack-format' }
            ] }
          ],
          '/en/reference/': [{ text: 'Reference', items: [
            { text: 'Public API', link: '/en/reference/api' },
            { text: 'Configuration', link: '/en/reference/configuration' },
            { text: 'Pack Format', link: '/en/reference/pack-format' }
          ] }],
          '/en/maintainers/': [{ text: 'Maintainers', items: [
            { text: 'Development and Architecture', link: '/en/maintainers/development' },
            { text: 'Testing and Release', link: '/en/maintainers/release' }
          ] }]
        },
        outline: { level: [2, 3], label: 'On this page' },
        editLink: { pattern: editLinkPattern, text: 'Edit this page on GitHub' },
        lastUpdated: { text: 'Last updated' },
        docFooter: { prev: 'Previous', next: 'Next' },
        darkModeSwitchLabel: 'Theme',
        sidebarMenuLabel: 'Menu',
        returnToTopLabel: 'Back to top',
        langMenuLabel: 'Change language',
        footer: { message: 'Development preview documentation for the latest implementation only.', copyright: 'Released under the MIT License.' }
      },
      ja: {
        label: '日本語',
        nav: [
          { text: 'プレイヤーガイド', link: '/ja/guide/getting-started' },
          { text: 'Mod 連携', link: '/ja/integration/getting-started' },
          { text: 'リファレンス', link: '/ja/reference/api' },
          { text: 'メンテナンス', link: '/ja/maintainers/development' },
          { text: 'v0.4.1 · API 4', link: '/ja/reference/api' }
        ],
        sidebar: {
          '/ja/guide/': [{ text: 'プレイヤーガイド', items: [
            { text: 'はじめに', link: '/ja/guide/getting-started' },
            { text: 'モデル管理', link: '/ja/guide/models' },
            { text: '表示とレンダリング', link: '/ja/guide/appearance' },
            { text: 'アクションとホットキー', link: '/ja/guide/actions' },
            { text: 'トラブルシューティング', link: '/ja/guide/troubleshooting' }
          ] }],
          '/ja/integration/': [
            { text: 'Mod 連携', items: [
              { text: '5 分で導入', link: '/ja/integration/getting-started' },
              { text: 'モデル制御', link: '/ja/integration/model-api' },
              { text: 'スレッドと高頻度更新', link: '/ja/integration/threading' },
              { text: 'モデル Pack の同梱', link: '/ja/integration/packs' }
            ] },
            { text: 'リファレンス', items: [
              { text: 'API リファレンス', link: '/ja/reference/api' },
              { text: 'Pack 形式', link: '/ja/reference/pack-format' }
            ] }
          ],
          '/ja/reference/': [{ text: 'リファレンス', items: [
            { text: '公開 API', link: '/ja/reference/api' },
            { text: '設定構造', link: '/ja/reference/configuration' },
            { text: 'Pack 形式', link: '/ja/reference/pack-format' }
          ] }],
          '/ja/maintainers/': [{ text: 'メンテナー', items: [
            { text: '開発とアーキテクチャ', link: '/ja/maintainers/development' },
            { text: 'テストとリリース', link: '/ja/maintainers/release' }
          ] }]
        },
        outline: { level: [2, 3], label: 'このページの内容' },
        editLink: { pattern: editLinkPattern, text: 'GitHub で編集' },
        lastUpdated: { text: '最終更新' },
        docFooter: { prev: '前へ', next: '次へ' },
        darkModeSwitchLabel: 'テーマ',
        sidebarMenuLabel: 'メニュー',
        returnToTopLabel: 'ページ上部へ',
        langMenuLabel: '言語を変更',
        footer: { message: '開発プレビューです。最新実装のみを説明しています。', copyright: 'Released under the MIT License.' }
      }
    }
  }
})
