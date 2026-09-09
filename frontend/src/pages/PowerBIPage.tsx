import { useEffect, useRef, useState } from 'react';
import { Alert, Button, Card, Descriptions, Layout, Result, Spin, Space, Tag, Typography } from 'antd';
import { ArrowRightOutlined, ReloadOutlined, DashboardOutlined } from '@ant-design/icons';
import { useNavigate } from 'react-router-dom';
import api from '../api';

const { Header, Content } = Layout;
const { Title, Text } = Typography;
const POWERBI_SDK_URL = 'https://cdn.jsdelivr.net/npm/powerbi-client@2.24.1/dist/powerbi.js';

type EmbedConfig = {
  reportId: string;
  embedUrl: string;
  accessToken: string;
  tokenExpiration?: string | null;
  datasetId?: string | null;
};

type PowerBIWindow = Window & {
  powerbi?: {
    embed: (element: HTMLElement, config: Record<string, unknown>) => any;
    reset: (element: HTMLElement) => void;
  };
};

function getStoredUser() {
  try {
    const raw = JSON.parse(localStorage.getItem('user') || '{}');
    return { ...raw, role: raw.role || raw.Role?.name || raw.Role || '' };
  } catch {
    return {};
  }
}

function loadPowerBISdk(): Promise<void> {
  const win = window as PowerBIWindow;
  if (win.powerbi) return Promise.resolve();

  return new Promise((resolve, reject) => {
    const existing = document.querySelector<HTMLScriptElement>('script[data-powerbi-sdk="true"]');
    if (existing) {
      existing.addEventListener('load', () => resolve(), { once: true });
      existing.addEventListener('error', () => reject(new Error('تعذر تحميل Power BI SDK.')), { once: true });
      return;
    }

    const script = document.createElement('script');
    script.src = POWERBI_SDK_URL;
    script.async = true;
    script.dataset.powerbiSdk = 'true';
    script.onload = () => resolve();
    script.onerror = () => reject(new Error('تعذر تحميل Power BI SDK.'));
    document.head.appendChild(script);
  });
}

export default function PowerBIPage() {
  const navigate = useNavigate();
  const user = getStoredUser();
  const containerRef = useRef<HTMLDivElement>(null);
  const reportRef = useRef<any>(null);
  const [config, setConfig] = useState<EmbedConfig | null>(null);
  const [loading, setLoading] = useState(true);
  const [embedding, setEmbedding] = useState(false);
  const [error, setError] = useState('');

  const load = async () => {
    setLoading(true);
    setError('');
    try {
      const response = await api.get<EmbedConfig>('/PowerBi/embed-config', { timeout: 30000 });
      setConfig(response.data);
    } catch (e: any) {
      setError(e.response?.data?.detail || e.response?.data?.message || 'تعذر تحميل تقرير Power BI.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (!localStorage.getItem('token')) {
      navigate('/');
      return;
    }
    if (!['Admin', 'Supervisor'].includes(user.role)) {
      navigate('/dashboard');
      return;
    }
    load();
  }, []);

  useEffect(() => {
    if (!config || !containerRef.current) return;

    let cancelled = false;
    const embed = async () => {
      setEmbedding(true);
      try {
        await loadPowerBISdk();
        if (cancelled || !containerRef.current) return;

        const win = window as PowerBIWindow;
        if (!win.powerbi) throw new Error('Power BI SDK غير متاح.');

        if (reportRef.current) {
          try { win.powerbi.reset(containerRef.current); } catch { /* no-op */ }
          reportRef.current = null;
        }

        reportRef.current = win.powerbi.embed(containerRef.current, {
          type: 'report',
          id: config.reportId,
          embedUrl: config.embedUrl,
          accessToken: config.accessToken,
          tokenType: 1,
          settings: {
            panes: {
              filters: { visible: true, expanded: false },
              pageNavigation: { visible: true },
            },
            navContentPaneEnabled: true,
          },
        });
      } catch (e: any) {
        if (!cancelled) setError(e.message || 'تعذر تضمين تقرير Power BI.');
      } finally {
        if (!cancelled) setEmbedding(false);
      }
    };

    embed();
    return () => {
      cancelled = true;
      if (containerRef.current) {
        try { (window as PowerBIWindow).powerbi?.reset(containerRef.current); } catch { /* no-op */ }
      }
      reportRef.current = null;
    };
  }, [config]);

  if (loading) return <div style={{ minHeight: '100vh', display: 'grid', placeItems: 'center' }}><Spin size="large" tip="جاري تحميل Power BI..." /></div>;

  return <Layout style={{ minHeight: '100vh' }}>
    <Header className="app-header">
      <Space><Button icon={<ArrowRightOutlined />} onClick={() => navigate('/dashboard')}>العودة</Button><Title level={4} style={{ color: '#fff', margin: 0 }}>Power BI — التحليلات المتقدمة</Title></Space>
      <Space><Text style={{ color: '#fff' }}>{user.fullName} — {user.role}</Text><Button icon={<ReloadOutlined />} onClick={load}>تحديث التقرير</Button><Button onClick={() => { localStorage.clear(); navigate('/'); }}>خروج</Button></Space>
    </Header>
    <Content className="page-content">
      {error ? <Result status="warning" title="Power BI غير جاهز بعد" subTitle={error} extra={<Space wrap><Button type="primary" icon={<ReloadOutlined />} onClick={load}>إعادة المحاولة</Button><Button onClick={() => navigate('/analytics')}>لوحة التحليلات الحالية</Button></Space>} /> : config ? <>
        <Card style={{ marginBottom: 16 }}>
          <Space direction="vertical" size={4}>
            <Space><DashboardOutlined /><Text strong>تقرير Power BI المدمج</Text><Tag color="green">App owns data</Tag></Space>
            <Text type="secondary">التقرير يُعرض داخل النظام باستخدام Embed Token من الـ Backend، ولا يتم إرسال Client Secret إلى المتصفح.</Text>
            {config.tokenExpiration && <Text type="secondary">انتهاء الرمز الحالي: {new Date(config.tokenExpiration).toLocaleString('ar')}</Text>}
          </Space>
        </Card>
        <Alert type="info" showIcon style={{ marginBottom: 16 }} message="طبقة التحليلات الرسمية" description="Power BI يستخدم Views مخصصة في MySQL لعرض مؤشرات التذاكر وSLA والأعطال الجماعية وأداء الفرق وتحليلات التصنيف الذكي." />
        <Card bodyStyle={{ padding: 0 }}>
          <div ref={containerRef} style={{ width: '100%', height: 'calc(100vh - 230px)', minHeight: 700, position: 'relative' }}>
            {embedding && <div style={{ position: 'absolute', inset: 0, display: 'grid', placeItems: 'center', zIndex: 1, background: 'rgba(255,255,255,.75)' }}><Spin size="large" tip="جاري عرض التقرير..." /></div>}
          </div>
        </Card>
        <Descriptions bordered size="small" style={{ marginTop: 16 }}>
          <Descriptions.Item label="Report ID">{config.reportId}</Descriptions.Item>
          {config.datasetId && <Descriptions.Item label="Dataset ID">{config.datasetId}</Descriptions.Item>}
        </Descriptions>
      </> : null}
    </Content>
  </Layout>;
}
