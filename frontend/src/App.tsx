import { useEffect, useMemo, useState } from 'react';
import { BrowserRouter, Routes, Route, Navigate, useNavigate } from 'react-router-dom';
import {
  ConfigProvider, Form, Input, Button, Card, Table, message, Layout, Typography, Tag, Space,
  Row, Col, Statistic, Progress, Empty, Modal, Select, Descriptions, List, Divider, Badge,
  Tabs, Timeline, Tooltip, Switch, InputNumber
} from 'antd';
import {
  LogoutOutlined, PlusOutlined, ReloadOutlined, CustomerServiceOutlined, DashboardOutlined,
  EyeOutlined, BulbOutlined, WarningOutlined, HistoryOutlined, UserSwitchOutlined,
  SendOutlined, TeamOutlined, UserOutlined, EditOutlined, UserAddOutlined, PoweroffOutlined, SearchOutlined
} from '@ant-design/icons';
import arEG from 'antd/locale/ar_EG';
import api from './api';
import PowerBIPage from './pages/PowerBIPage';

const { Header, Content } = Layout;
const { Title, Text } = Typography;

function getStoredUser() {
  try {
    const raw = JSON.parse(localStorage.getItem('user') || '{}');
    return { ...raw, role: raw.role || raw.Role?.name || raw.Role || '' };
  } catch { return {}; }
}

const statusLabel: Record<string, string> = {
  New: 'جديدة', Classified: 'مصنفة', Assigned: 'مُسندة', InProgress: 'قيد المعالجة',
  PendingCustomer: 'بانتظار العميل', Resolved: 'تم الحل', Closed: 'مغلقة', Cancelled: 'ملغاة'
};
const priorityLabel: Record<string, string> = { Low: 'منخفضة', Medium: 'متوسطة', High: 'عالية', Critical: 'حرجة' };
const priorityColor: Record<string, string> = { Low: 'default', Medium: 'blue', High: 'orange', Critical: 'red' };
const statusColor: Record<string, string> = { New: 'blue', Classified: 'cyan', Assigned: 'purple', InProgress: 'processing', PendingCustomer: 'gold', Resolved: 'success', Closed: 'default', Cancelled: 'error' };

const allowedTransitions: Record<string, string[]> = {
  New: ['Classified', 'Cancelled'], Classified: ['Assigned', 'Cancelled'], Assigned: ['InProgress', 'Cancelled'],
  InProgress: ['PendingCustomer', 'Resolved', 'Cancelled'], PendingCustomer: ['InProgress', 'Resolved', 'Cancelled'],
  Resolved: ['Closed'], Closed: [], Cancelled: []
};

function LoginPage() {
  const navigate = useNavigate();
  const [loading, setLoading] = useState(false);
  const submit = async (values: any) => {
    setLoading(true);
    try {
      const r = await api.post('/Auth/login', values);
      localStorage.setItem('token', r.data.token);
      localStorage.setItem('user', JSON.stringify(r.data.user));
      navigate('/dashboard');
      message.success('تم تسجيل الدخول بنجاح');
    } catch {
      message.error('بيانات الدخول غير صحيحة');
    } finally { setLoading(false); }
  };
  return <div className="login-page"><Card className="login-card">
    <div style={{ textAlign: 'center', marginBottom: 25 }}>
      <CustomerServiceOutlined style={{ fontSize: 52, color: '#1677ff' }} />
      <Title level={3}>نظام إدارة المشاكل التقنية</Title>
      <Text type="secondary">ISP Smart Ticketing & Data Science</Text>
    </div>
    <Form layout="vertical" size="large" onFinish={submit}>
      <Form.Item name="email" label="البريد الإلكتروني" initialValue="admin@isp.com" rules={[{ required: true, message: 'أدخل البريد' }]}><Input /></Form.Item>
      <Form.Item name="password" label="كلمة المرور" initialValue="123456" rules={[{ required: true, message: 'أدخل كلمة المرور' }]}><Input.Password /></Form.Item>
      <Button type="primary" htmlType="submit" block loading={loading}>تسجيل الدخول</Button>
    </Form>
  </Card></div>;
}

function Dashboard() {
  const navigate = useNavigate();
  const [tickets, setTickets] = useState<any[]>([]);
  const [summary, setSummary] = useState<any>();
  const [outages, setOutages] = useState<any[]>([]);
  const [users, setUsers] = useState<any[]>([]);
  const [teams, setTeams] = useState<any[]>([]);
  const [loading, setLoading] = useState(false);
  const [form] = Form.useForm();
  const [assignForm] = Form.useForm();
  const [selected, setSelected] = useState<any>();
  const [recommendations, setRecommendations] = useState<any[]>([]);
  const [history, setHistory] = useState<any[]>([]);
  const [modalOpen, setModalOpen] = useState(false);
  const [assignOpen, setAssignOpen] = useState(false);
  const [historyOpen, setHistoryOpen] = useState(false);
  const [statusLoading, setStatusLoading] = useState(false);
  const [assignLoading, setAssignLoading] = useState(false);
  const [historyLoading, setHistoryLoading] = useState(false);
  const [commentLoading, setCommentLoading] = useState(false);
  const [commentForm] = Form.useForm();

  const user = getStoredUser();
  const canAssign = user.role === 'Admin' || user.role === 'Supervisor';

  const load = async () => {
    setLoading(true);
    try {
      const requests: Promise<any>[] = [api.get('/Tickets'), api.get('/Analytics/summary'), api.get('/Analytics/outages')];
      if (canAssign) { requests.push(api.get('/Teams')); requests.push(api.get('/Users')); }
      const result = await Promise.all(requests);
      setTickets(result[0].data?.items || result[0].data || []); setSummary(result[1].data); setOutages(result[2].data || []);
      if (canAssign) { setTeams(result[3].data || []); setUsers(result[4].data || []); }
    } catch { message.error('تعذر تحميل بيانات النظام'); }
    finally { setLoading(false); }
  };

  useEffect(() => {
    if (!localStorage.getItem('token')) navigate('/'); else load();
  }, []);

  const create = async (v: any) => {
    try {
      const r = await api.post('/Tickets', { ...v, source: 'Phone' });
      message.success(`تم إنشاء ${r.data.ticketNumber} وتحليلها آلياً`);
      form.resetFields(); await load();
      if (r.data.recommendations?.length) { setSelected(r.data); setRecommendations(r.data.recommendations); setModalOpen(true); }
    } catch (e: any) { message.error(e.response?.data?.message || 'تعذر إنشاء التذكرة'); }
  };

  const openTicket = async (id: number) => {
    try {
      const [t, r] = await Promise.all([api.get(`/Tickets/${id}`), api.get(`/Tickets/${id}/recommendations`)]);
      setSelected(t.data); setRecommendations(r.data || []); setModalOpen(true);
    } catch (e: any) { message.error(e.response?.data?.message || 'تعذر تحميل تفاصيل التذكرة'); }
  };

  const changeStatus = async (status: string) => {
    if (!selected || status === selected.status) return;
    setStatusLoading(true);
    try {
      await api.patch(`/Tickets/${selected.id}/status`, { status });
      message.success(`تم تغيير الحالة إلى ${statusLabel[status]}`);
      setSelected({ ...selected, status }); await load(); await loadHistory(selected.id, false);
    } catch (e: any) { message.error(e.response?.data?.message || 'تعذر تحديث الحالة'); }
    finally { setStatusLoading(false); }
  };

  const loadHistory = async (id: number, show = true) => {
    setHistoryLoading(true);
    try { const r = await api.get(`/Tickets/${id}/history`); setHistory(r.data || []); if (show) setHistoryOpen(true); }
    catch (e: any) { message.error(e.response?.data?.message || 'تعذر تحميل سجل التذكرة'); }
    finally { setHistoryLoading(false); }
  };

  const openAssign = () => {
    if (!selected) return;
    assignForm.setFieldsValue({ teamId: selected.teamId, assignedToId: selected.assignedTo?.id || selected.assignedToId });
    setAssignOpen(true);
  };

  const assign = async (v: any) => {
    if (!selected) return;
    setAssignLoading(true);
    try {
      const r = await api.patch(`/Tickets/${selected.id}/assignment`, v);
      message.success(`تم إسناد ${selected.ticketNumber} إلى ${r.data.assignedTo}`);
      setAssignOpen(false); await openTicket(selected.id); await load(); await loadHistory(selected.id, false);
    } catch (e: any) { message.error(e.response?.data?.message || 'تعذر إسناد التذكرة'); }
    finally { setAssignLoading(false); }
  };

  const addComment = async (v: any) => {
    if (!selected) return;
    setCommentLoading(true);
    try { await api.post(`/Tickets/${selected.id}/comments`, v); commentForm.resetFields(); message.success('تمت إضافة التعليق'); await openTicket(selected.id); }
    catch (e: any) { message.error(e.response?.data?.message || 'تعذر إضافة التعليق'); }
    finally { setCommentLoading(false); }
  };

  const logout = () => { localStorage.clear(); navigate('/'); };
  const agentsByTeam = useMemo(() => users.filter(x => x.role === 'Agent' && x.isActive), [users]);
  const openTickets = tickets.filter(t => !['Closed', 'Resolved', 'Cancelled'].includes(t.status)).length;

  const columns = [
    { title: 'رقم التذكرة', dataIndex: 'ticketNumber', render: (v: string) => <Text strong>{v}</Text> },
    { title: 'العميل', dataIndex: 'customerName' }, { title: 'المشكلة', dataIndex: 'title', ellipsis: true },
    { title: 'التصنيف', dataIndex: 'category', render: (v: string) => v || 'غير مصنف' },
    { title: 'الأولوية', dataIndex: 'priority', render: (v: string) => <Tag color={priorityColor[v]}>{priorityLabel[v] || v}</Tag> },
    { title: 'الحالة', dataIndex: 'status', render: (v: string) => <Tag color={statusColor[v]}>{statusLabel[v] || v}</Tag> },
    { title: 'الفريق', dataIndex: 'team', render: (v: string) => v || '—' },
    { title: 'الموظف', dataIndex: 'assignedTo', render: (v: string) => v || '—' },
    { title: 'ثقة AI', dataIndex: 'aiConfidence', render: (v: number) => v == null ? '—' : `${(v * 100).toFixed(0)}%` },
    { title: 'إجراء', render: (_: any, r: any) => <Space><Button icon={<EyeOutlined />} onClick={() => openTicket(r.id)}>تفاصيل</Button>{canAssign && <Tooltip title="إسناد / إعادة إسناد"><Button icon={<UserSwitchOutlined />} onClick={async () => { await openTicket(r.id); setTimeout(() => setAssignOpen(true), 0); }}>إسناد</Button></Tooltip>}</Space> }
  ];

  return <Layout style={{ minHeight: '100vh' }}><Header className="app-header">
    <Space size="middle"><DashboardOutlined style={{ color: '#fff' }} /><Title level={4} style={{ color: '#fff', margin: 0 }}>ISP Smart Ticketing</Title><Button size="small" onClick={()=>navigate('/tickets')}>بحث التذاكر</Button><Button size="small" onClick={()=>navigate('/outages')}>الأعطال الجماعية</Button><Button size="small" onClick={()=>navigate('/knowledge')}>قاعدة المعرفة</Button><Button type="primary" size="small" onClick={() => navigate('/dashboard')}>لوحة التذاكر</Button><Button size="small" onClick={() => navigate('/teams')}>الفرق</Button>{['Admin','Supervisor'].includes(user.role) && <Button size="small" onClick={() => navigate('/ml')}>الذكاء الاصطناعي</Button>}{['Admin','Supervisor'].includes(user.role) && <Button size="small" onClick={() => navigate('/analytics')}>التحليلات</Button>}{['Admin','Supervisor'].includes(user.role) && <Button size="small" type="primary" onClick={() => navigate('/powerbi')}>Power BI</Button>}{user.role === 'Admin' && <Button size="small" onClick={() => navigate('/users')}>المستخدمون</Button>}</Space>
    <Space><Text style={{ color: '#fff' }}>{user.fullName || 'المستخدم'} — {user.role || ''}</Text><Button icon={<LogoutOutlined />} onClick={logout}>خروج</Button></Space>
  </Header><Content className="page-content">
    <Row gutter={[16, 16]} style={{ marginBottom: 16 }}>{[['إجمالي التذاكر', summary?.total || 0], ['المفتوحة', summary?.open ?? openTickets], ['تم حلها', summary?.resolved || 0], ['خرق SLA', summary?.slaBreached || 0], ['أعطال جماعية', summary?.activeOutages || 0]].map(([title, value]) => <Col xs={24} sm={12} lg={Math.floor(24 / 5)} key={String(title)}><Card><Statistic title={title as string} value={value as number} /></Card></Col>)}</Row>
    <Row gutter={16}><Col xs={24} lg={8}>
      <Card title="مؤشرات علم البيانات" style={{ marginBottom: 16 }}><div className="metric"><Text>معدل التصنيف الآلي</Text><Progress percent={summary?.classificationRate || 0} /></div><div className="metric"><Text>معدل الإسناد</Text><Progress percent={summary?.assignmentRate || 0} /></div><div className="metric"><Text>معدل الحل</Text><Progress percent={summary?.resolutionRate || 0} /></div><Text type="secondary">متوسط زمن الحل: {summary?.averageResolutionMinutes || 0} دقيقة</Text></Card>
      <Card title="التوزيع حسب التصنيف" style={{ marginBottom: 16 }}>{summary?.byCategory?.length ? summary.byCategory.map((x: any) => <div key={x.category} className="distribution-row"><span>{x.category}</span><Tag>{x.count}</Tag></div>) : <Empty description="لا توجد بيانات" />}</Card>
      <Card title={<Space><WarningOutlined /> الأعطال الجماعية النشطة</Space>}>{outages.filter(x => !['Resolved', 'Closed'].includes(x.status)).length ? outages.filter(x => !['Resolved', 'Closed'].includes(x.status)).slice(0, 5).map((x: any) => <Card size="small" key={x.id} style={{ marginBottom: 8 }}><Space direction="vertical" size={2}><Text strong>{x.title}</Text><Text>{x.region || 'غير محدد'} — {x.category || 'كل التصنيفات'}</Text><Tag color={x.severity === 'Critical' ? 'red' : x.severity === 'High' ? 'orange' : 'gold'}>{x.severity}</Tag><Text type="secondary">عملاء متأثرون تقديرياً: {x.estimatedAffectedCustomers}</Text></Space></Card>) : <Empty description="لا توجد أعطال جماعية نشطة" />}</Card>
    </Col><Col xs={24} lg={16}>
      <Card title="إنشاء تذكرة جديدة" style={{ marginBottom: 16 }}><Form form={form} layout="vertical" onFinish={create}><Row gutter={12}><Col xs={24} md={12}><Form.Item name="customerId" label="معرف العميل" rules={[{ required: true }]}><Input placeholder="CUST-1001" /></Form.Item></Col><Col xs={24} md={12}><Form.Item name="customerName" label="اسم العميل" rules={[{ required: true }]}><Input /></Form.Item></Col><Col xs={24} md={12}><Form.Item name="customerPhone" label="الهاتف"><Input /></Form.Item></Col><Col xs={24} md={12}><Form.Item name="customerRegion" label="المنطقة"><Input placeholder="دمشق" /></Form.Item></Col><Col xs={24}><Form.Item name="title" label="عنوان المشكلة" rules={[{ required: true }]}><Input /></Form.Item></Col><Col xs={24}><Form.Item name="description" label="وصف المشكلة" rules={[{ required: true }]}><Input.TextArea rows={3} /></Form.Item></Col></Row><Button type="primary" htmlType="submit" icon={<PlusOutlined />}>إنشاء وتحليل التذكرة</Button></Form></Card>
      <Card title="آخر التذاكر" extra={<Button icon={<ReloadOutlined />} onClick={load}>تحديث</Button>}><Table rowKey="id" loading={loading} dataSource={tickets} columns={columns} pagination={{ pageSize: 10 }} scroll={{ x: 1200 }} /></Card>
    </Col></Row>
  </Content>

  <Modal title={selected ? `تفاصيل ${selected.ticketNumber || ''}` : 'تفاصيل التذكرة'} open={modalOpen} onCancel={() => setModalOpen(false)} footer={null} width={950} destroyOnHidden>
    {selected && <Tabs items={[
      { key: 'details', label: 'التفاصيل', children: <>
        <Descriptions bordered size="small" column={2}><Descriptions.Item label="العميل">{selected.customerName}</Descriptions.Item><Descriptions.Item label="المنطقة">{selected.customerRegion || '—'}</Descriptions.Item><Descriptions.Item label="العنوان" span={2}>{selected.title}</Descriptions.Item><Descriptions.Item label="الوصف" span={2}>{selected.description}</Descriptions.Item><Descriptions.Item label="التصنيف">{selected.category?.name || selected.category || '—'}</Descriptions.Item><Descriptions.Item label="الأولوية"><Tag color={priorityColor[selected.priority]}>{priorityLabel[selected.priority] || selected.priority}</Tag></Descriptions.Item><Descriptions.Item label="الفريق">{selected.team?.name || selected.team || '—'}</Descriptions.Item><Descriptions.Item label="الموظف">{selected.assignedTo?.fullName || selected.assignedTo || '—'}</Descriptions.Item><Descriptions.Item label="ثقة AI">{selected.aiConfidence == null ? '—' : `${(selected.aiConfidence * 100).toFixed(1)}%`}</Descriptions.Item><Descriptions.Item label="موعد SLA">{selected.slaDueAt ? new Date(selected.slaDueAt).toLocaleString('ar') : '—'}</Descriptions.Item></Descriptions>
        <Divider><BulbOutlined /> اقتراحات قاعدة المعرفة</Divider>
        {recommendations.length ? <List dataSource={recommendations} renderItem={(r: any) => <List.Item><List.Item.Meta title={<Space>{r.titleAr}<Badge count={`${Math.round(r.relevance * 100)}%`} /></Space>} description={<><Text>{r.solution}</Text><List size="small" dataSource={r.steps} renderItem={(step: string) => <List.Item>• {step}</List.Item>} /></>} /></List.Item>} /> : <Empty description="لا توجد توصيات مناسبة" />}
        <Divider>إجراءات التذكرة</Divider>
        <Space wrap>
          <Select value={selected.status} loading={statusLoading} style={{ minWidth: 210 }} onChange={changeStatus} options={allowedTransitions[selected.status]?.map(s => ({ value: s, label: statusLabel[s] })) || []} placeholder="اختر الحالة التالية" />
          {canAssign && <Button type="primary" icon={<UserSwitchOutlined />} onClick={openAssign}>إسناد / إعادة إسناد</Button>}
          <Button icon={<HistoryOutlined />} loading={historyLoading} onClick={() => loadHistory(selected.id)}>سجل التذكرة</Button>
          {selected.isSlaBreached && <Tag color="red">SLA متجاوز</Tag>}{selected.outageEventId && <Tag color="orange">مرتبطة بعطل جماعي</Tag>}
        </Space>
      </> },
      { key: 'attachments', label: `المرفقات (${selected.attachments?.length || 0})`, children: <AttachmentPanel ticket={selected} onRefresh={() => openTicket(selected.id)} /> },
      { key: 'comments', label: `التعليقات (${selected.comments?.length || 0})`, children: <>
        {selected.comments?.length ? <List dataSource={selected.comments} renderItem={(c: any) => <List.Item><List.Item.Meta avatar={<UserOutlined />} title={<Space>{c.user || 'مستخدم'} <Tag>{c.isInternal ? 'داخلي' : 'ظاهر للعميل'}</Tag></Space>} description={<><div>{c.comment}</div><Text type="secondary">{new Date(c.createdAt).toLocaleString('ar')}</Text></>} /></List.Item>} /> : <Empty description="لا توجد تعليقات" />}
        <Divider />
        <Form form={commentForm} layout="vertical" onFinish={addComment}><Form.Item name="comment" label="إضافة تعليق" rules={[{ required: true }]}><Input.TextArea rows={3} /></Form.Item><Form.Item name="isInternal" valuePropName="checked" initialValue={true} label="تعليق داخلي"><Switch /></Form.Item><Button type="primary" htmlType="submit" loading={commentLoading} icon={<SendOutlined />}>إضافة التعليق</Button></Form>
      </> }
    ]} />}
  </Modal>

  <Modal title={<Space><UserSwitchOutlined /> إسناد / إعادة إسناد التذكرة</Space>} open={assignOpen} onCancel={() => setAssignOpen(false)} onOk={() => assignForm.submit()} confirmLoading={assignLoading} okText="حفظ الإسناد" cancelText="إلغاء" destroyOnHidden>
    <Form form={assignForm} layout="vertical" onFinish={assign}>
      <Form.Item name="teamId" label="الفريق" rules={[{ required: true, message: 'اختر الفريق' }]}><Select placeholder="اختر الفريق" options={teams.filter(t => t.isActive).map(t => ({ value: t.id, label: <Space><TeamOutlined />{t.name}</Space> }))} onChange={() => assignForm.setFieldValue('assignedToId', undefined)} /></Form.Item>
      <Form.Item shouldUpdate={(prev, cur) => prev.teamId !== cur.teamId}>{() => { const teamId = assignForm.getFieldValue('teamId'); const teamAgents = agentsByTeam.filter(a => a.teamId === teamId); return <Form.Item name="assignedToId" label="الموظف" rules={[{ required: true, message: 'اختر الموظف' }]}><Select placeholder="اختر الموظف" options={teamAgents.map(a => ({ value: a.id, label: `${a.fullName} (${a.employeeCode})` }))} notFoundContent="لا يوجد موظفون فعالون بهذا الفريق" /></Form.Item>; }}</Form.Item>
      <Form.Item name="notes" label="ملاحظات الإسناد"><Input.TextArea rows={3} placeholder="سبب إعادة الإسناد أو ملاحظة للفريق" /></Form.Item>
    </Form>
  </Modal>

  <Modal title={<Space><HistoryOutlined /> سجل التذكرة {selected?.ticketNumber}</Space>} open={historyOpen} onCancel={() => setHistoryOpen(false)} footer={null} width={800}>
    {history.length ? <Timeline items={history.map((h: any) => ({ key: h.id, children: <><Text strong>{h.action}</Text>{h.oldValue && <> — {h.oldValue} → {h.newValue}</>}<div>{h.notes || ''}</div><Text type="secondary">{new Date(h.createdAt).toLocaleString('ar')} — {h.changedBy || `User #${h.changedById}`}</Text></> }))} /> : <Empty description="لا يوجد سجل بعد" />}
  </Modal>
  </Layout>;
}



function AttachmentPanel({ ticket, onRefresh }: { ticket: any; onRefresh: () => void }) {
  const [loading,setLoading]=useState(false);
  const upload=async(e:any)=>{const file=e.target.files?.[0]; if(!file)return; const fd=new FormData(); fd.append('file',file); setLoading(true); try{await api.post(`/Tickets/${ticket.id}/attachments`,fd,{headers:{'Content-Type':'multipart/form-data'}});message.success('تم رفع المرفق');onRefresh()}catch(err:any){message.error(err.response?.data?.message||'تعذر رفع المرفق')}finally{setLoading(false);e.target.value=''}};
  const download=async(id:number,name:string)=>{try{const r=await api.get(`/Tickets/${ticket.id}/attachments/${id}`,{responseType:'blob'});const url=URL.createObjectURL(r.data);const a=document.createElement('a');a.href=url;a.download=name;a.click();URL.revokeObjectURL(url)}catch(err:any){message.error('تعذر تنزيل المرفق')}};
  return <div><Space style={{marginBottom:12}}><label><Button icon={<PlusOutlined />} loading={loading}>رفع ملف<input type="file" hidden onChange={upload} /></Button></label><Text type="secondary">الحد الأقصى 10MB — الأنواع: PDF, DOCX, XLSX, JPG, PNG, TXT, LOG</Text></Space>{ticket.attachments?.length?<List dataSource={ticket.attachments} renderItem={(a:any)=><List.Item actions={[<Button onClick={()=>download(a.id,a.fileName)}>تنزيل</Button>]}><List.Item.Meta title={a.fileName} description={`${a.fileSize || 0} bytes — ${new Date(a.createdAt).toLocaleString('ar')}`} /></List.Item>}/>:<Empty description="لا توجد مرفقات"/>}</div>;
}

function MlPage() {
  const navigate = useNavigate();
  const user = getStoredUser();
  const [report, setReport] = useState<any>(null);
  const [loading, setLoading] = useState(false);
  const [testing, setTesting] = useState(false);
  const [result, setResult] = useState<any>(null);
  const [comparison, setComparison] = useState<any[]>([]);
  const [form] = Form.useForm();

  const loadReport = async () => {
    setLoading(true);
    try {
      const r = await api.get('/Ml/training-report');
      setReport(r.data);
    } catch (e: any) {
      message.error(e.response?.data?.detail || e.response?.data?.message || 'تعذر تحميل تقرير نموذج الذكاء الاصطناعي');
    } finally { setLoading(false); }
  };

  useEffect(() => {
    if (!localStorage.getItem('token')) navigate('/');
    else if (!['Admin', 'Supervisor'].includes(user.role)) navigate('/dashboard');
    else { loadReport(); api.get('/Ml/compare').then(r => setComparison(r.data || [])).catch(() => {}); }
  }, []);

  const testClassification = async (v: any) => {
    setTesting(true);
    setResult(null);
    try {
      const r = await api.post('/Ml/classify-test', v);
      setResult(r.data);
      message.success('تم اختبار التصنيف بنجاح');
    } catch (e: any) {
      message.error(e.response?.data?.detail || e.response?.data?.message || 'تعذر اختبار التصنيف');
    } finally { setTesting(false); }
  };

  const pct = (v: number | undefined) => v == null ? 0 : Math.round(v * 10000) / 100;

  return <Layout style={{ minHeight: '100vh' }}><Header className="app-header">
    <Space><Button onClick={() => navigate('/dashboard')}>لوحة التذاكر</Button><Title level={4} style={{ color: '#fff', margin: 0 }}>الذكاء الاصطناعي وتحليل البيانات</Title></Space>
    <Space><Text style={{ color: '#fff' }}>{user.fullName || 'المستخدم'} — {user.role || ''}</Text><Button icon={<LogoutOutlined />} onClick={() => { localStorage.clear(); navigate('/'); }}>خروج</Button></Space>
  </Header><Content className="page-content">
    <Row gutter={[16, 16]}>
      <Col xs={24} lg={16}>
        <Card title="تقرير تدريب نموذج تصنيف التذاكر" extra={<Button icon={<ReloadOutlined />} onClick={loadReport} loading={loading}>تحديث التقرير</Button>}>
          {report ? <>
            <Row gutter={[12, 12]}>
              <Col xs={24} sm={12} lg={6}><Card size="small"><Statistic title="الدقة Accuracy" value={pct(report.accuracy)} suffix="%" /></Card></Col>
              <Col xs={24} sm={12} lg={6}><Card size="small"><Statistic title="Macro Accuracy" value={pct(report.macroAccuracy)} suffix="%" /></Card></Col>
              <Col xs={24} sm={12} lg={6}><Card size="small"><Statistic title="Log Loss" value={report.logLoss} precision={3} /></Card></Col>
              <Col xs={24} sm={12} lg={6}><Card size="small"><Statistic title="تحسن Log Loss" value={pct(report.logLossReduction)} suffix="%" /></Card></Col>
            </Row>
            <Divider />
            <Descriptions bordered size="small" column={2}>
              <Descriptions.Item label="إصدار النموذج">{report.modelVersion}</Descriptions.Item>
              <Descriptions.Item label="إجمالي العينات">{report.totalSamples}</Descriptions.Item>
              <Descriptions.Item label="عينات التدريب">{report.trainingSamples}</Descriptions.Item>
              <Descriptions.Item label="عينات الاختبار">{report.testSamples}</Descriptions.Item>
              <Descriptions.Item label="وقت إنشاء التقرير" span={2}>{report.generatedAtUtc ? new Date(report.generatedAtUtc).toLocaleString('ar') : '—'}</Descriptions.Item>
            </Descriptions>
            <Divider />
            <Title level={5}>أداء النموذج حسب الفئة</Title>
            <Table
              size="small"
              pagination={false}
              rowKey="category"
              dataSource={report.classMetrics || []}
              columns={[
                { title: 'الفئة', dataIndex: 'category' },
                { title: 'Support', dataIndex: 'support' },
                { title: 'Precision', dataIndex: 'precision', render: (v: number) => `${(v * 100).toFixed(1)}%` },
                { title: 'Recall', dataIndex: 'recall', render: (v: number) => `${(v * 100).toFixed(1)}%` },
                { title: 'F1', dataIndex: 'f1Score', render: (v: number) => `${(v * 100).toFixed(1)}%` },
              ]}
            />
            <Divider />
            <Title level={5}>Confusion Matrix</Title>
            {report.confusionMatrix?.length ? (
              <Table
                size="small"
                pagination={false}
                scroll={{ x: true }}
                dataSource={report.confusionMatrix.map((row: number[], i: number) => {
                  const item: any = { key: i, actual: report.classNames?.[i] || `Class ${i + 1}` };
                  row.forEach((v, j) => { item[`c${j}`] = v; });
                  return item;
                })}
                columns={[
                  { title: 'Actual \ Predicted', dataIndex: 'actual', fixed: 'left' },
                  ...(report.classNames || []).map((name: string, i: number) => ({ title: name, dataIndex: `c${i}` }))
                ]}
              />
            ) : <Empty description="لا توجد مصفوفة أخطاء" />}
            <Divider />
            <Title level={5}>مقارنة خوارزميات التصنيف</Title>
            <Table size="small" pagination={false} rowKey="algorithm" dataSource={comparison} columns={[
              { title: "الخوارزمية", dataIndex: "algorithm" },
              { title: "Accuracy", dataIndex: "accuracy", render: (v:number) => `${(v*100).toFixed(1)}%` },
              { title: "Macro Accuracy", dataIndex: "macroAccuracy", render: (v:number) => `${(v*100).toFixed(1)}%` },
              { title: "Log Loss", dataIndex: "logLoss", render: (v:number) => v.toFixed(3) },
              { title: "Log Loss Reduction", dataIndex: "logLossReduction", render: (v:number) => `${(v*100).toFixed(1)}%` }
            ]} />
            <Divider />
            <Text type="secondary">الدقة الحالية هي نتيجة اختبار Dataset المشروع المكوّن من 180 عينة، وليست بديلاً عن التقييم على بيانات ISP حقيقية مجهّلة.</Text>
          </> : <Empty description="لا يوجد تقرير" />}
        </Card>
      </Col>
      <Col xs={24} lg={8}>
        <Card title="اختبار التصنيف الآلي">
          <Form form={form} layout="vertical" onFinish={testClassification}>
            <Form.Item name="title" label="عنوان المشكلة" rules={[{ required: true, message: 'أدخل عنوان المشكلة' }]}>
              <Input placeholder="مثال: الانترنت مقطوع" />
            </Form.Item>
            <Form.Item name="description" label="وصف المشكلة" rules={[{ required: true, message: 'أدخل وصف المشكلة' }]}>
              <Input.TextArea rows={5} placeholder="اكتب وصفاً واضحاً للمشكلة..." />
            </Form.Item>
            <Button type="primary" htmlType="submit" block loading={testing}>اختبار التصنيف</Button>
          </Form>
        </Card>
      </Col>
      {result && <Col xs={24}>
        <Card title="نتيجة اختبار الذكاء الاصطناعي">
          <Row gutter={[12, 12]}>
            <Col xs={24} sm={12} lg={4}><Statistic title="التصنيف" value={result.category} /></Col>
            <Col xs={24} sm={12} lg={4}><Statistic title="الأولوية" value={priorityLabel[result.priority] || result.priority} /></Col>
            <Col xs={24} sm={12} lg={4}><Statistic title="SLA" value={result.slaHours} suffix="ساعة" /></Col>
            <Col xs={24} sm={12} lg={4}><Statistic title="ثقة AI" value={result.confidencePercent} suffix="%" /></Col>
            <Col xs={24} sm={12} lg={4}><Statistic title="زمن المعالجة" value={result.processingTimeMs} suffix="ms" /></Col>
            <Col xs={24} sm={12} lg={4}><Statistic title="الإصدار" value={result.modelVersion} /></Col>
          </Row>
          <Divider />
          <Progress percent={result.confidencePercent} status={result.confidencePercent >= 70 ? 'success' : 'normal'} />
        </Card>
      </Col>}
    </Row>
  </Content></Layout>;
}


function AnalyticsPage() {
  const navigate = useNavigate();
  const user = getStoredUser();
  const [summary, setSummary] = useState<any>(null);
  const [agents, setAgents] = useState<any[]>([]);
  const [trends, setTrends] = useState<any[]>([]);
  const [sla, setSla] = useState<any>(null);
  const [loading, setLoading] = useState(false);
  const [days, setDays] = useState(30);

  const load = async () => {
    setLoading(true);
    try {
      const [s, a, t, sl] = await Promise.all([
        api.get('/Analytics/summary'),
        api.get('/Analytics/agent-performance'),
        api.get(`/Analytics/trends?days=${days}`),
        api.get('/Analytics/sla')
      ]);
      setSummary(s.data);
      setAgents(a.data || []);
      setTrends(t.data || []);
      setSla(sl.data);
    } catch (e: any) {
      if ([401, 403].includes(e.response?.status)) navigate('/dashboard');
      else message.error(e.response?.data?.message || 'تعذر تحميل التحليلات');
    } finally { setLoading(false); }
  };

  useEffect(() => {
    if (!localStorage.getItem('token')) navigate('/');
    else if (!['Admin', 'Supervisor'].includes(user.role)) navigate('/dashboard');
    else load();
  }, [days]);

  return <Layout style={{ minHeight: '100vh' }}><Header className="app-header">
    <Space><Button onClick={() => navigate('/dashboard')}>لوحة التذاكر</Button><Title level={4} style={{ color: '#fff', margin: 0 }}>لوحة التحليلات ومؤشرات الأداء</Title></Space>
    <Space><Text style={{ color: '#fff' }}>{user.fullName || 'المستخدم'} — {user.role || ''}</Text><Button icon={<LogoutOutlined />} onClick={() => { localStorage.clear(); navigate('/'); }}>خروج</Button></Space>
  </Header><Content className="page-content">
    <Card loading={loading} title="الفترة الزمنية" extra={<Button icon={<ReloadOutlined />} onClick={load}>تحديث</Button>} style={{ marginBottom: 16 }}>
      <Space wrap><Text>عرض الاتجاهات لآخر:</Text><Select value={days} onChange={setDays} options={[7, 14, 30, 60, 90].map(x => ({ value: x, label: `${x} يوم` }))} /></Space>
    </Card>

    <Row gutter={[16, 16]} style={{ marginBottom: 16 }}>
      <Col xs={24} sm={12} lg={6}><Card><Statistic title="إجمالي التذاكر" value={summary?.total || 0} /></Card></Col>
      <Col xs={24} sm={12} lg={6}><Card><Statistic title="نسبة الحل" value={summary?.resolutionRate || 0} suffix="%" /></Card></Col>
      <Col xs={24} sm={12} lg={6}><Card><Statistic title="متوسط زمن الحل" value={summary?.averageResolutionMinutes || 0} suffix="دقيقة" /></Card></Col>
      <Col xs={24} sm={12} lg={6}><Card><Statistic title="الالتزام بـ SLA" value={sla?.complianceRate || 0} suffix="%" /></Card></Col>
    </Row>

    <Row gutter={[16, 16]}>
      <Col xs={24} lg={12}>
        <Card title="اتجاه التذاكر اليومية" loading={loading}>
          <Table size="small" rowKey="date" dataSource={[...trends].reverse()} pagination={{ pageSize: 10 }} columns={[
            { title: 'التاريخ', dataIndex: 'date' },
            { title: 'منشأة', dataIndex: 'created' },
            { title: 'محلولة', dataIndex: 'resolved' },
            { title: 'مفتوحة', dataIndex: 'open' }
          ]} />
        </Card>
      </Col>
      <Col xs={24} lg={12}>
        <Card title="مؤشرات SLA" loading={loading}>
          <Descriptions bordered size="small" column={1}>
            <Descriptions.Item label="إجمالي التذاكر">{sla?.total ?? 0}</Descriptions.Item>
            <Descriptions.Item label="التذاكر المتجاوزة لـ SLA">{sla?.breached ?? 0}</Descriptions.Item>
            <Descriptions.Item label="التذاكر المحلولة">{sla?.resolved ?? 0}</Descriptions.Item>
            <Descriptions.Item label="المحلولة ضمن SLA">{sla?.onTimeResolved ?? 0}</Descriptions.Item>
            <Descriptions.Item label="نسبة الالتزام الإجمالية"><Progress percent={sla?.complianceRate || 0} /></Descriptions.Item>
            <Descriptions.Item label="نسبة الحل ضمن SLA"><Progress percent={sla?.resolvedOnTimeRate || 0} /></Descriptions.Item>
          </Descriptions>
        </Card>
      </Col>
    </Row>

    <Card title="أداء موظفي الدعم" loading={loading} style={{ marginTop: 16 }}>
      <Table rowKey="id" dataSource={agents} pagination={{ pageSize: 10 }} scroll={{ x: 950 }} columns={[
        { title: 'الموظف', dataIndex: 'fullName', render: (v: string, r: any) => <Space direction="vertical" size={0}><Text strong>{v}</Text><Text type="secondary">{r.employeeCode}</Text></Space> },
        { title: 'الفريق', dataIndex: 'team', render: (v: string) => v || '—' },
        { title: 'المسندة', dataIndex: 'assigned' },
        { title: 'المفتوحة', dataIndex: 'open' },
        { title: 'المحلولة', dataIndex: 'resolved' },
        { title: 'خرق SLA', dataIndex: 'slaBreached' },
        { title: 'معدل الحل', dataIndex: 'resolutionRate', render: (v: number) => <Progress percent={v || 0} size="small" /> },
        { title: 'الحمل الحالي', dataIndex: 'loadPercent', render: (v: number) => <Progress percent={Math.min(v || 0, 100)} size="small" status={(v || 0) >= 100 ? 'exception' : 'active'} /> }
      ]} />
    </Card>

    <Row gutter={[16, 16]} style={{ marginTop: 16 }}>
      <Col xs={24} lg={8}><Card title="حسب الأولوية">{summary?.byPriority?.length ? summary.byPriority.map((x: any) => <div key={x.priority} className="distribution-row"><span>{priorityLabel[x.priority] || x.priority}</span><Tag color={priorityColor[x.priority]}>{x.count}</Tag></div>) : <Empty description="لا توجد بيانات" />}</Card></Col>
      <Col xs={24} lg={8}><Card title="حسب الحالة">{summary?.byStatus?.length ? summary.byStatus.map((x: any) => <div key={x.status} className="distribution-row"><span>{statusLabel[x.status] || x.status}</span><Tag>{x.count}</Tag></div>) : <Empty description="لا توجد بيانات" />}</Card></Col>
      <Col xs={24} lg={8}><Card title="حسب المنطقة">{summary?.byRegion?.length ? summary.byRegion.map((x: any) => <div key={x.region} className="distribution-row"><span>{x.region}</span><Tag>{x.count}</Tag></div>) : <Empty description="لا توجد بيانات" />}</Card></Col>
    </Row>
  </Content></Layout>;
}


function UsersPage() {
  const navigate = useNavigate();
  const user = JSON.parse(localStorage.getItem('user') || '{}');
  const [users, setUsers] = useState<any[]>([]);
  const [teams, setTeams] = useState<any[]>([]);
  const [loading, setLoading] = useState(false);
  const [modalOpen, setModalOpen] = useState(false);
  const [editing, setEditing] = useState<any>(null);
  const [form] = Form.useForm();

  const load = async () => {
    setLoading(true);
    try {
      const [u, t] = await Promise.all([api.get('/Users'), api.get('/Teams')]);
      setUsers(u.data || []); setTeams(t.data || []);
    } catch (e: any) {
      if (e.response?.status === 401 || e.response?.status === 403) navigate('/dashboard');
      else message.error(e.response?.data?.message || 'تعذر تحميل المستخدمين');
    } finally { setLoading(false); }
  };

  useEffect(() => {
    if (!localStorage.getItem('token')) navigate('/');
    else if (user.role !== 'Admin') navigate('/dashboard');
    else load();
  }, []);

  const openCreate = () => {
    setEditing(null); form.resetFields(); form.setFieldsValue({ role: 'Agent', isActive: true, maxConcurrentTickets: 10 }); setModalOpen(true);
  };
  const openEdit = (record: any) => {
    setEditing(record); form.setFieldsValue({ ...record, password: undefined, role: record.role, teamId: record.teamId ?? undefined }); setModalOpen(true);
  };
  const save = async (values: any) => {
    try {
      if (editing) await api.put(`/Users/${editing.id}`, values);
      else await api.post('/Users', values);
      message.success(editing ? 'تم تعديل المستخدم بنجاح' : 'تم إنشاء المستخدم بنجاح');
      setModalOpen(false); await load();
    } catch (e: any) { message.error(e.response?.data?.message || 'تعذر حفظ المستخدم'); }
  };
  const toggle = async (record: any) => {
    try { await api.patch(`/Users/${record.id}/status`, { isActive: !record.isActive }); message.success(record.isActive ? 'تم تعطيل المستخدم' : 'تم تفعيل المستخدم'); await load(); }
    catch (e: any) { message.error(e.response?.data?.message || 'تعذر تغيير حالة المستخدم'); }
  };

  const columns = [
    { title: 'الكود', dataIndex: 'employeeCode' },
    { title: 'الاسم', dataIndex: 'fullName', render: (v: string) => <Text strong>{v}</Text> },
    { title: 'البريد', dataIndex: 'email' },
    { title: 'الدور', dataIndex: 'role', render: (v: string) => <Tag color={v === 'Admin' ? 'red' : v === 'Supervisor' ? 'purple' : 'blue'}>{v}</Tag> },
    { title: 'الفريق', dataIndex: 'team', render: (v: string) => v || '—' },
    { title: 'الحد الأقصى للتذاكر', dataIndex: 'maxConcurrentTickets' },
    { title: 'الحالة', dataIndex: 'isActive', render: (v: boolean) => <Tag color={v ? 'success' : 'error'}>{v ? 'فعال' : 'معطل'}</Tag> },
    { title: 'إجراء', render: (_: any, r: any) => <Space><Button icon={<EditOutlined />} onClick={() => openEdit(r)}>تعديل</Button><Button icon={<PoweroffOutlined />} danger={r.isActive} onClick={() => toggle(r)}>{r.isActive ? 'تعطيل' : 'تفعيل'}</Button></Space> }
  ];

  return <Layout style={{ minHeight: '100vh' }}><Header className="app-header">
    <Space><Button onClick={() => navigate('/dashboard')}>لوحة التذاكر</Button><Title level={4} style={{ color: '#fff', margin: 0 }}>إدارة المستخدمين</Title></Space>
    <Space><Text style={{ color: '#fff' }}>{user.fullName} — {user.role}</Text><Button icon={<LogoutOutlined />} onClick={() => { localStorage.clear(); navigate('/'); }}>خروج</Button></Space>
  </Header><Content className="page-content">
    <Card title="المستخدمون والموظفون" extra={<Space><Button icon={<ReloadOutlined />} onClick={load}>تحديث</Button><Button type="primary" icon={<UserAddOutlined />} onClick={openCreate}>مستخدم جديد</Button></Space>}>
      <Table rowKey="id" loading={loading} dataSource={users} columns={columns} pagination={{ pageSize: 12 }} scroll={{ x: 1100 }} />
    </Card>
    <Modal title={editing ? 'تعديل المستخدم' : 'إضافة مستخدم جديد'} open={modalOpen} onCancel={() => setModalOpen(false)} onOk={() => form.submit()} width={650} okText="حفظ" cancelText="إلغاء">
      <Form form={form} layout="vertical" onFinish={save}>
        <Row gutter={12}>
          <Col xs={24} md={12}><Form.Item name="employeeCode" label="كود الموظف" rules={[{ required: true }]}><Input /></Form.Item></Col>
          <Col xs={24} md={12}><Form.Item name="fullName" label="الاسم الكامل" rules={[{ required: true }]}><Input /></Form.Item></Col>
          <Col xs={24} md={12}><Form.Item name="email" label="البريد الإلكتروني" rules={[{ required: true, type: 'email' }]}><Input /></Form.Item></Col>
          <Col xs={24} md={12}><Form.Item name="phone" label="الهاتف"><Input /></Form.Item></Col>
          <Col xs={24} md={12}><Form.Item name="password" label={editing ? 'كلمة المرور الجديدة (اختياري)' : 'كلمة المرور'} rules={editing ? [] : [{ required: true, min: 6 }]}><Input.Password /></Form.Item></Col>
          <Col xs={24} md={12}><Form.Item name="role" label="الدور" rules={[{ required: true }]}><Select options={[{ value: 'Admin', label: 'Admin' }, { value: 'Supervisor', label: 'Supervisor' }, { value: 'Agent', label: 'Agent' }]} /></Form.Item></Col>
          <Col xs={24} md={12}><Form.Item name="teamId" label="الفريق"><Select allowClear placeholder="بدون فريق" options={teams.filter(t => t.isActive).map(t => ({ value: t.id, label: t.name }))} /></Form.Item></Col>
          <Col xs={24} md={12}><Form.Item name="maxConcurrentTickets" label="الحد الأقصى للتذاكر المتزامنة" rules={[{ required: true, type: 'number', min: 1, max: 100 }]}><InputNumber min={1} max={100} style={{ width: '100%' }} /></Form.Item></Col>
          <Col xs={24}><Form.Item name="isActive" valuePropName="checked"><Switch checkedChildren="فعال" unCheckedChildren="معطل" /></Form.Item></Col>
        </Row>
      </Form>
    </Modal>
  </Content></Layout>;
}

function TeamsPage() {
  const navigate = useNavigate();
  const user = getStoredUser();
  const canManage = user.role === 'Admin' || user.role === 'Supervisor';
  const [teams, setTeams] = useState<any[]>([]);
  const [users, setUsers] = useState<any[]>([]);
  const [loading, setLoading] = useState(false);
  const [modalOpen, setModalOpen] = useState(false);
  const [editing, setEditing] = useState<any>(null);
  const [members, setMembers] = useState<any[]>([]);
  const [membersOpen, setMembersOpen] = useState(false);
  const [form] = Form.useForm();

  const load = async () => {
    setLoading(true);
    try { const [t, u] = await Promise.all([api.get('/Teams'), api.get('/Users')]); setTeams(t.data || []); setUsers(u.data || []); }
    catch (e: any) { if (e.response?.status === 401) navigate('/'); else message.error(e.response?.data?.message || 'تعذر تحميل الفرق'); }
    finally { setLoading(false); }
  };
  useEffect(() => { if (!localStorage.getItem('token')) navigate('/'); else load(); }, []);

  const openCreate = () => { setEditing(null); form.resetFields(); setModalOpen(true); };
  const openEdit = (record: any) => { setEditing(record); form.setFieldsValue({ name: record.name, description: record.description, managerId: record.managerId ?? undefined, isActive: record.isActive }); setModalOpen(true); };
  const save = async (values: any) => {
    try { if (editing) await api.put(`/Teams/${editing.id}`, values); else await api.post('/Teams', values); message.success(editing ? 'تم تعديل الفريق' : 'تم إنشاء الفريق'); setModalOpen(false); await load(); }
    catch (e: any) { message.error(e.response?.data?.message || 'تعذر حفظ الفريق'); }
  };
  const showMembers = async (id: number) => { try { const r = await api.get(`/Teams/${id}`); setMembers(r.data?.members || []); setMembersOpen(true); } catch (e: any) { message.error(e.response?.data?.message || 'تعذر تحميل أعضاء الفريق'); } };

  const columns = [
    { title: 'الفريق', dataIndex: 'name', render: (v: string) => <Text strong>{v}</Text> },
    { title: 'الوصف', dataIndex: 'description', render: (v: string) => v || '—' },
    { title: 'المدير', dataIndex: 'manager', render: (v: string) => v || '—' },
    { title: 'الأعضاء الفعالون', dataIndex: 'memberCount' },
    { title: 'الحالة', dataIndex: 'isActive', render: (v: boolean) => <Tag color={v ? 'success' : 'error'}>{v ? 'فعال' : 'معطل'}</Tag> },
    { title: 'إجراء', render: (_: any, r: any) => <Space><Button icon={<EyeOutlined />} onClick={() => showMembers(r.id)}>الأعضاء</Button>{canManage && <Button icon={<EditOutlined />} onClick={() => openEdit(r)}>تعديل</Button>}</Space> }
  ];

  return <Layout style={{ minHeight: '100vh' }}><Header className="app-header">
    <Space><Button onClick={() => navigate('/dashboard')}>لوحة التذاكر</Button><Title level={4} style={{ color: '#fff', margin: 0 }}>إدارة الفرق</Title></Space>
    <Space><Text style={{ color: '#fff' }}>{user.fullName} — {user.role}</Text><Button icon={<LogoutOutlined />} onClick={() => { localStorage.clear(); navigate('/'); }}>خروج</Button></Space>
  </Header><Content className="page-content">
    <Card title="الفرق التشغيلية" extra={<Space><Button icon={<ReloadOutlined />} onClick={load}>تحديث</Button>{canManage && <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>فريق جديد</Button>}</Space>}>
      <Table rowKey="id" loading={loading} dataSource={teams} columns={columns} pagination={false} scroll={{ x: 950 }} />
    </Card>
    <Modal title={editing ? 'تعديل الفريق' : 'إضافة فريق جديد'} open={modalOpen} onCancel={() => setModalOpen(false)} onOk={() => form.submit()} okText="حفظ" cancelText="إلغاء">
      <Form form={form} layout="vertical" onFinish={save}>
        <Form.Item name="name" label="اسم الفريق" rules={[{ required: true }]}><Input /></Form.Item>
        <Form.Item name="description" label="الوصف"><Input.TextArea rows={3} /></Form.Item>
        <Form.Item name="managerId" label="مدير الفريق"><Select allowClear placeholder="اختر المدير" options={users.filter(u => u.isActive && ['Supervisor', 'Admin'].includes(u.role)).map(u => ({ value: u.id, label: `${u.fullName} — ${u.role}` }))} /></Form.Item>
        {editing && <Form.Item name="isActive" valuePropName="checked"><Switch checkedChildren="فعال" unCheckedChildren="معطل" /></Form.Item>}
      </Form>
    </Modal>
    <Modal title="أعضاء الفريق" open={membersOpen} onCancel={() => setMembersOpen(false)} footer={null} width={800}>
      <Table rowKey="id" dataSource={members} pagination={false} columns={[{ title: 'الكود', dataIndex: 'employeeCode' }, { title: 'الاسم', dataIndex: 'fullName' }, { title: 'البريد', dataIndex: 'email' }, { title: 'الدور', dataIndex: 'role' }, { title: 'الحد الأقصى', dataIndex: 'maxConcurrentTickets' }]} />
    </Modal>
  </Content></Layout>;
}


function TicketsPage() {
  const navigate = useNavigate();
  const user = getStoredUser();
  const [data, setData] = useState<any>({ items: [], total: 0, page: 1, pageSize: 20 });
  const [loading, setLoading] = useState(false);
  const [filters, setFilters] = useState<any>({});
  const [teams, setTeams] = useState<any[]>([]);
  const [agents, setAgents] = useState<any[]>([]);
  const [categories, setCategories] = useState<any[]>([]);
  const [form] = Form.useForm();

  const load = async (page = 1) => {
    setLoading(true);
    try {
      const params = { ...filters, page, pageSize: 20 };
      Object.keys(params).forEach(k => { if (params[k] === undefined || params[k] === '' || params[k] === null) delete params[k]; });
      const r = await api.get('/Tickets', { params }); setData(r.data);
    } catch (e: any) { if (e.response?.status === 401) navigate('/'); else message.error(e.response?.data?.message || 'تعذر تحميل التذاكر'); }
    finally { setLoading(false); }
  };
  useEffect(() => { if (!localStorage.getItem('token')) navigate('/'); else { load(); Promise.all([api.get('/Teams'), api.get('/Users'), api.get('/Categories')]).then(([t,u,c]) => { setTeams(t.data || []); setAgents((u.data || []).filter((x:any) => x.role === 'Agent' && x.isActive)); setCategories(c.data || []); }).catch(() => {}); } }, []);
  const submit = (v:any) => { setFilters(v); setTimeout(() => load(1), 0); };
  const columns = [
    { title: 'رقم', dataIndex: 'ticketNumber', render: (v:string) => <Text strong>{v}</Text> },
    { title: 'العميل', dataIndex: 'customerName' }, { title: 'المشكلة', dataIndex: 'title', ellipsis: true },
    { title: 'التصنيف', dataIndex: 'category', render: (v:string) => v || 'غير مصنف' },
    { title: 'الأولوية', dataIndex: 'priority', render: (v:string) => <Tag color={priorityColor[v]}>{priorityLabel[v] || v}</Tag> },
    { title: 'الحالة', dataIndex: 'status', render: (v:string) => <Tag color={statusColor[v]}>{statusLabel[v] || v}</Tag> },
    { title: 'الفريق', dataIndex: 'team', render: (v:string) => v || '—' }, { title: 'الموظف', dataIndex: 'assignedTo', render: (v:string) => v || '—' },
    { title: 'SLA', dataIndex: 'isSlaBreached', render: (v:boolean) => v ? <Tag color="red">متجاوز</Tag> : <Tag color="green">ضمن SLA</Tag> }
  ];
  return <Layout style={{minHeight:'100vh'}}><Header className="app-header"><Space><Button onClick={() => navigate('/dashboard')}>لوحة التذاكر</Button><Title level={4} style={{color:'#fff',margin:0}}>البحث المتقدم في التذاكر</Title></Space><Space><Text style={{color:'#fff'}}>{user.fullName} — {user.role}</Text><Button icon={<LogoutOutlined/>} onClick={()=>{localStorage.clear();navigate('/')}}>خروج</Button></Space></Header><Content className="page-content">
    <Card title={<Space><SearchOutlined/> البحث والفلترة</Space>} style={{marginBottom:16}}><Form form={form} layout="vertical" onFinish={submit}><Row gutter={12}>
      <Col xs={24} md={8}><Form.Item name="search" label="بحث"><Input placeholder="رقم التذكرة، العميل، المشكلة..." allowClear/></Form.Item></Col>
      <Col xs={24} md={4}><Form.Item name="status" label="الحالة"><Select allowClear options={Object.keys(statusLabel).map(x=>({value:x,label:statusLabel[x]}))}/></Form.Item></Col>
      <Col xs={24} md={4}><Form.Item name="priority" label="الأولوية"><Select allowClear options={Object.keys(priorityLabel).map(x=>({value:x,label:priorityLabel[x]}))}/></Form.Item></Col>
      <Col xs={24} md={4}><Form.Item name="category" label="التصنيف"><Select allowClear options={categories.map(x=>({value:x.name,label:x.name}))}/></Form.Item></Col>
      <Col xs={24} md={4}><Form.Item name="region" label="المنطقة"><Input allowClear/></Form.Item></Col>
      <Col xs={24} md={8}><Form.Item name="teamId" label="الفريق"><Select allowClear options={teams.filter(x=>x.isActive).map(x=>({value:x.id,label:x.name}))}/></Form.Item></Col>
      <Col xs={24} md={8}><Form.Item name="assignedToId" label="الموظف"><Select allowClear options={agents.map(x=>({value:x.id,label:x.fullName}))}/></Form.Item></Col>
      <Col xs={24} md={8}><Form.Item name="slaBreached" label="SLA"><Select allowClear options={[{value:true,label:'متجاوز'},{value:false,label:'ضمن SLA'}]}/></Form.Item></Col>
      <Col xs={24}><Space><Button type="primary" htmlType="submit" icon={<SearchOutlined/>}>بحث</Button><Button onClick={()=>{form.resetFields();setFilters({});load(1)}}>مسح الفلاتر</Button><Button icon={<ReloadOutlined/>} onClick={()=>load(data.page)}>تحديث</Button></Space></Col>
    </Row></Form></Card>
    <Card title={`نتائج التذاكر (${data.total || 0})`}><Table rowKey="id" loading={loading} dataSource={data.items || []} columns={columns} scroll={{x:1200}} pagination={{current:data.page,pageSize:data.pageSize,total:data.total,showSizeChanger:false,onChange:(p)=>load(p)}} onRow={(record: any)=>({onClick:()=>navigate(`/dashboard?ticket=${record.id}`)})}/></Card>
  </Content></Layout>;
}

function OutagesPage() {
  const navigate=useNavigate(); const user=getStoredUser(); const [rows,setRows]=useState<any[]>([]); const [loading,setLoading]=useState(false); const [editing,setEditing]=useState<any>(null); const [form]=Form.useForm();
  const load=async()=>{setLoading(true);try{const r=await api.get('/Outages');setRows(r.data||[])}catch(e:any){message.error(e.response?.data?.message||'تعذر تحميل الأعطال الجماعية')}finally{setLoading(false)}};
  useEffect(()=>{if(!localStorage.getItem('token'))navigate('/');else load()},[]);
  const save=async(v:any)=>{try{await api.patch(`/Outages/${editing.id}`,v);message.success('تم تحديث العطل الجماعي');setEditing(null);load()}catch(e:any){message.error(e.response?.data?.message||'تعذر تحديث العطل')}};
  return <Layout style={{minHeight:'100vh'}}><Header className="app-header"><Space><Button onClick={()=>navigate('/dashboard')}>لوحة التذاكر</Button><Title level={4} style={{color:'#fff',margin:0}}>إدارة الأعطال الجماعية</Title></Space><Space><Text style={{color:'#fff'}}>{user.fullName} — {user.role}</Text><Button icon={<LogoutOutlined/>} onClick={()=>{localStorage.clear();navigate('/')}}>خروج</Button></Space></Header><Content className="page-content"><Card title="الأعطال المكتشفة" extra={<Button icon={<ReloadOutlined/>} onClick={load}>تحديث</Button>}><Table rowKey="id" loading={loading} dataSource={rows} scroll={{x:1100}} columns={[{title:'العطل',dataIndex:'title'},{title:'المنطقة',dataIndex:'region'},{title:'التصنيف',dataIndex:'category',render:(v:string)=>v||'—'},{title:'الخطورة',dataIndex:'severity',render:(v:string)=><Tag color={v==='Critical'?'red':v==='High'?'orange':'gold'}>{v}</Tag>},{title:'الحالة',dataIndex:'status'},{title:'المتأثرون',dataIndex:'estimatedAffectedCustomers'},{title:'وقت الاكتشاف',dataIndex:'detectedAt',render:(v:string)=>new Date(v).toLocaleString('ar')},{title:'إجراء',render:(_:any,r:any)=><Button icon={<EditOutlined/>} onClick={()=>{setEditing(r);form.setFieldsValue({status:r.status,severity:r.severity,estimatedAffectedCustomers:r.estimatedAffectedCustomers,rootCause:r.rootCause,description:r.description})}}>تحديث</Button>}]} /></Card><Modal open={!!editing} title="تحديث العطل الجماعي" onCancel={()=>setEditing(null)} onOk={()=>form.submit()} okText="حفظ" cancelText="إلغاء"><Form form={form} layout="vertical" onFinish={save}><Form.Item name="status" label="الحالة"><Select options={['Detected','Investigating','Resolved','Closed'].map(x=>({value:x,label:x}))}/></Form.Item><Form.Item name="severity" label="الخطورة"><Select options={['Low','Medium','High','Critical'].map(x=>({value:x,label:x}))}/></Form.Item><Form.Item name="estimatedAffectedCustomers" label="عدد المتأثرين"><InputNumber min={0} style={{width:'100%'}}/></Form.Item><Form.Item name="rootCause" label="السبب الجذري"><Input.TextArea rows={3}/></Form.Item><Form.Item name="description" label="الوصف"><Input.TextArea rows={3}/></Form.Item></Form></Modal></Content></Layout>;
}

function KnowledgePage() {
  const navigate=useNavigate(); const user=getStoredUser(); const canManage=['Admin','Supervisor'].includes(user.role); const [rows,setRows]=useState<any[]>([]); const [loading,setLoading]=useState(false); const [editing,setEditing]=useState<any>(null); const [modalOpen,setModalOpen]=useState(false); const [form]=Form.useForm();
  const load=async()=>{setLoading(true);try{const r=await api.get('/KnowledgeBase');setRows(r.data||[])}catch(e:any){message.error(e.response?.data?.message||'تعذر تحميل قاعدة المعرفة')}finally{setLoading(false)}};
  useEffect(()=>{if(!localStorage.getItem('token'))navigate('/');else load()},[]);
  const save=async(v:any)=>{const payload={...v,steps:(v.stepsText||'').split('\n').map((x:string)=>x.trim()).filter(Boolean)};delete payload.stepsText;try{if(editing)await api.put(`/KnowledgeBase/${editing.id}`,payload);else await api.post('/KnowledgeBase',payload);message.success('تم حفظ المقالة');setEditing(null);setModalOpen(false);form.resetFields();load()}catch(e:any){message.error(e.response?.data?.message||'تعذر حفظ المقالة')}};
  return <Layout style={{minHeight:'100vh'}}><Header className="app-header"><Space><Button onClick={()=>navigate('/dashboard')}>لوحة التذاكر</Button><Title level={4} style={{color:'#fff',margin:0}}>قاعدة المعرفة الذكية</Title></Space><Space><Text style={{color:'#fff'}}>{user.fullName} — {user.role}</Text><Button icon={<LogoutOutlined/>} onClick={()=>{localStorage.clear();navigate('/')}}>خروج</Button></Space></Header><Content className="page-content"><Card title="مقالات الحلول" extra={<Space><Button icon={<ReloadOutlined/>} onClick={load}>تحديث</Button>{canManage&&<Button type="primary" icon={<PlusOutlined/>} onClick={()=>{setEditing(null);form.resetFields();form.setFieldsValue({isActive:true});setModalOpen(true);}}>مقالة جديدة</Button>}</Space>}><Table rowKey="id" loading={loading} dataSource={rows} scroll={{x:1100}} columns={[{title:'العنوان',dataIndex:'titleAr',render:(v:string,r:any)=><Space direction="vertical" size={0}><Text strong>{v}</Text><Text type="secondary">{r.title}</Text></Space>},{title:'التصنيف',dataIndex:'category'},{title:'الحالة',dataIndex:'isActive',render:(v:boolean)=><Tag color={v?'green':'red'}>{v?'فعال':'معطل'}</Tag>},{title:'الحل',dataIndex:'solution',ellipsis:true},{title:'إجراء',render:(_:any,r:any)=><Space>{canManage&&<><Button icon={<EditOutlined/>} onClick={()=>{setEditing(r);form.setFieldsValue({...r,stepsText:(r.steps||[]).join('\n')});setModalOpen(true)}}>تعديل</Button><Button onClick={async()=>{try{await api.patch(`/KnowledgeBase/${r.id}/status`,{isActive:!r.isActive});load()}catch(e:any){message.error(e.response?.data?.message||'تعذر تغيير الحالة')}}}>{r.isActive?'تعطيل':'تفعيل'}</Button></>}</Space>}]} /></Card><Modal open={modalOpen} title={editing?'تعديل مقالة':'إضافة مقالة'} onCancel={()=>{setEditing(null);setModalOpen(false);form.resetFields()}} onOk={()=>form.submit()} width={700} okText="حفظ" cancelText="إلغاء"><Form form={form} layout="vertical" onFinish={save}><Form.Item name="title" label="العنوان الإنجليزي" rules={[{required:true}]}><Input/></Form.Item><Form.Item name="titleAr" label="العنوان العربي" rules={[{required:true}]}><Input/></Form.Item><Form.Item name="category" label="التصنيف" rules={[{required:true}]}><Select options={['Service Outage','Slow Speed','Router Issues','WiFi Problems','Billing','Other'].map(x=>({value:x,label:x}))}/></Form.Item><Form.Item name="summary" label="ملخص"><Input.TextArea rows={2}/></Form.Item><Form.Item name="solution" label="الحل" rules={[{required:true}]}><Input.TextArea rows={4}/></Form.Item><Form.Item name="stepsText" label="خطوات الحل (كل خطوة بسطر)"><Input.TextArea rows={5}/></Form.Item><Form.Item name="isActive" valuePropName="checked"><Switch checkedChildren="فعال" unCheckedChildren="معطل"/></Form.Item></Form></Modal></Content></Layout>;
}

export default function App() { return <ConfigProvider direction="rtl" locale={arEG}><BrowserRouter><Routes><Route path="/" element={<LoginPage />} /><Route path="/dashboard" element={<Dashboard />} /><Route path="/users" element={<UsersPage />} /><Route path="/teams" element={<TeamsPage />} /><Route path="/ml" element={<MlPage />} /><Route path="/analytics" element={<AnalyticsPage />} /><Route path="/powerbi" element={<PowerBIPage />} /><Route path="/tickets" element={<TicketsPage />} /><Route path="/outages" element={<OutagesPage />} /><Route path="/knowledge" element={<KnowledgePage />} /><Route path="*" element={<Navigate to="/" replace />} /></Routes></BrowserRouter></ConfigProvider>; }
