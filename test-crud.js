/**
 * UI CRUD 全系統測試腳本
 * 直接呼叫 Service 層，模擬 UI 操作的完整 CRUD 流程
 * 執行: node test-crud.js
 */
// Load .env settings
require('dotenv').config();

const { closePool } = require('./dist-electron/db/connection');
const { adminsService } = require('./dist-electron/services/admins.service');
const { login, logout, getSession } = require('./dist-electron/services/auth.service');
const { believersService } = require('./dist-electron/services/believers.service');
const { ceremonyCategorysService } = require('./dist-electron/services/ceremony-categories.service');
const { signupsService } = require('./dist-electron/services/signups.service');
const { signupLogsService } = require('./dist-electron/services/signup-logs.service');
const crypto = require('crypto');

// Test tracking
let passed = 0, failed = 0;
const issues = [];

function assert(condition, testId, desc, detail) {
  if (condition) {
    console.log(`  \x1b[32mPASS\x1b[0m ${testId}: ${desc}`);
    passed++;
  } else {
    console.log(`  \x1b[31mFAIL\x1b[0m ${testId}: ${desc}${detail ? ' => ' + detail : ''}`);
    failed++;
    issues.push({ testId, desc, detail });
  }
}

async function run() {
  console.log('='.repeat(60));
  console.log(' 法會報名系統 - CRUD 全系統測試');
  console.log('='.repeat(60));

  // Track created IDs for cleanup
  let testAdminId = null;
  let testAdmin2Id = null;
  let categoryRootId = null;
  let categoryRoot2Id = null;
  let categoryChildId1 = null;
  let categoryChildId2 = null;
  let believerId1 = null;
  let believerId2 = null;
  let believerId3 = null;  // for FK test
  let signupId1 = null;
  let signupId2 = null;
  let signupId3 = null;
  let logId1 = null;

  try {
    // =============================================
    // Phase 1: 登入 (Auth)
    // =============================================
    console.log('\n--- Phase 1: 登入 (Auth) ---');

    // TC-1.1: 空白帳密
    const r1_1 = await login('', '');
    assert(!r1_1.success, 'TC-1.1', '空白帳密登入失敗', r1_1.message);

    // TC-1.2: 錯誤密碼
    const r1_2 = await login('admin', 'wrongpass');
    assert(!r1_2.success, 'TC-1.2', '錯誤密碼登入失敗', r1_2.message);

    // TC-1.3: 正確登入
    const r1_3 = await login('admin', 'admin');
    assert(r1_3.success && r1_3.data, 'TC-1.3', '正確帳密登入成功', r1_3.message);
    if (r1_3.success) {
      assert(r1_3.data.Username === 'admin', 'TC-1.3b', '登入回傳正確 Username');
    }

    // TC-1.4: 取得 session
    const r1_4 = getSession();
    assert(r1_4.success && r1_4.data && r1_4.data.Username === 'admin', 'TC-1.4', 'Session 正確');

    // TC-1.5: 登出
    const r1_5 = logout();
    assert(r1_5.success, 'TC-1.5', '登出成功');
    const r1_5b = getSession();
    assert(r1_5b.data === null, 'TC-1.5b', '登出後 session 為 null');

    // 重新登入供後續測試
    await login('admin', 'admin');

    // =============================================
    // Phase 2: 管理者 (Admins) CRUD
    // =============================================
    console.log('\n--- Phase 2: 管理者 CRUD ---');

    // TC-2.1: List (READ)
    const r2_1 = await adminsService.getAll();
    assert(r2_1.success && r2_1.data.length >= 1, 'TC-2.1', `管理者列表載入 (${r2_1.data?.length} 筆)`);

    // TC-2.2: Create - 缺必填欄位
    const r2_2 = await adminsService.create({ Name: '測試', Username: '', Password: '' });
    // Note: DB may allow empty string, so check behavior
    if (r2_2.success) {
      // cleanup if accidentally created
      await adminsService.remove(r2_2.data.AdminID);
      assert(false, 'TC-2.2', '空白帳密應被拒絕', '資料庫允許空字串 Username - 前端需驗證');
    } else {
      assert(true, 'TC-2.2', '空白帳密建立失敗 (正確行為)');
    }

    // TC-2.3: Create
    const r2_3 = await adminsService.create({
      Name: '測試管理員',
      Username: 'testadmin1',
      Password: 'test123',
      IsEnabled: true,
    });
    assert(r2_3.success && r2_3.data, 'TC-2.3', '新增管理者成功');
    testAdminId = r2_3.data?.AdminID;

    // TC-2.4: Create second (for delete test)
    const r2_4 = await adminsService.create({
      Name: '待刪管理員',
      Username: 'deleteadmin',
      Password: 'del123',
      IsEnabled: true,
    });
    assert(r2_4.success, 'TC-2.4', '新增第二個管理者成功');
    testAdmin2Id = r2_4.data?.AdminID;

    // TC-2.5: Read by ID
    if (testAdminId) {
      const r2_5 = await adminsService.getById(testAdminId);
      assert(r2_5.success && r2_5.data.Name === '測試管理員', 'TC-2.5', '讀取管理者 by ID 正確');
    }

    // TC-2.6: Update
    if (testAdminId) {
      const r2_6 = await adminsService.update(testAdminId, { Name: '測試管理員-已修改', IsEnabled: false });
      assert(r2_6.success && r2_6.data.Name === '測試管理員-已修改', 'TC-2.6', '更新管理者成功');
    }

    // TC-2.7: 停用帳號登入測試
    logout();
    const r2_7 = await login('testadmin1', 'test123');
    assert(!r2_7.success, 'TC-2.7', '停用帳號無法登入', r2_7.message);
    // 重新以 admin 登入
    await login('admin', 'admin');

    // TC-2.8: 重新啟用後登入
    if (testAdminId) {
      await adminsService.update(testAdminId, { IsEnabled: true });
      logout();
      const r2_8 = await login('testadmin1', 'test123');
      assert(r2_8.success, 'TC-2.8', '啟用後帳號可登入');
      await login('admin', 'admin'); // switch back
    }

    // TC-2.9: Delete
    if (testAdmin2Id) {
      const r2_9 = await adminsService.remove(testAdmin2Id);
      assert(r2_9.success, 'TC-2.9', '刪除管理者成功');
      testAdmin2Id = null; // cleaned
      // verify
      const verify = await adminsService.getById(r2_4.data.AdminID);
      assert(!verify.success || !verify.data, 'TC-2.9b', '刪除後查不到資料');
    }

    // TC-2.10: 重複 Username 測試
    const r2_10 = await adminsService.create({
      Name: '重複帳號', Username: 'admin', Password: '123', IsEnabled: true,
    });
    if (r2_10.success) {
      // cleanup
      await adminsService.remove(r2_10.data.AdminID);
      assert(false, 'TC-2.10', '重複 Username 應被拒絕', 'DB 無 UNIQUE 約束，允許重複帳號');
    } else {
      assert(true, 'TC-2.10', '重複 Username 被拒絕');
    }

    // =============================================
    // Phase 3: 法會類別 (Ceremony Categories) CRUD
    // =============================================
    console.log('\n--- Phase 3: 法會類別 CRUD ---');

    // TC-3.1: 初始樹狀結構
    const r3_1 = await ceremonyCategorysService.getTree();
    assert(r3_1.success, 'TC-3.1', `取得樹狀結構 (${r3_1.data?.length} 個根節點)`);

    // TC-3.2: Create 根類別
    categoryRootId = crypto.randomUUID();
    const r3_2 = await ceremonyCategorysService.create({
      CeremonyCategoryID: categoryRootId,
      Title: '大悲懺',
      ParentID: null,
      Sort: 1,
    });
    assert(r3_2.success, 'TC-3.2', '新增根類別「大悲懺」成功');

    // TC-3.3: Create 第二個根類別
    categoryRoot2Id = crypto.randomUUID();
    const r3_3 = await ceremonyCategorysService.create({
      CeremonyCategoryID: categoryRoot2Id,
      Title: '梁皇寶懺',
      ParentID: null,
      Sort: 2,
    });
    assert(r3_3.success, 'TC-3.3', '新增根類別「梁皇寶懺」成功');

    // TC-3.4: Create 子類別
    categoryChildId1 = crypto.randomUUID();
    const r3_4 = await ceremonyCategorysService.create({
      CeremonyCategoryID: categoryChildId1,
      Title: '大悲懺-消災',
      ParentID: categoryRootId,
      Sort: 1,
    });
    assert(r3_4.success, 'TC-3.4', '新增子類別「大悲懺-消災」成功');

    categoryChildId2 = crypto.randomUUID();
    const r3_4b = await ceremonyCategorysService.create({
      CeremonyCategoryID: categoryChildId2,
      Title: '大悲懺-超薦',
      ParentID: categoryRootId,
      Sort: 2,
    });
    assert(r3_4b.success, 'TC-3.4b', '新增子類別「大悲懺-超薦」成功');

    // TC-3.5: 驗證樹狀結構
    const r3_5 = await ceremonyCategorysService.getTree();
    if (r3_5.success) {
      const root = r3_5.data.find(n => n.CeremonyCategoryID.toLowerCase() === categoryRootId.toLowerCase());
      assert(root && root.children.length === 2, 'TC-3.5', `大悲懺有 ${root?.children?.length || 0} 個子節點`);
    }

    // TC-3.6: getNextSort
    const r3_6 = await ceremonyCategorysService.getNextSort(categoryRootId);
    assert(r3_6.success && r3_6.data === 3, 'TC-3.6', `下一個排序值 = ${r3_6.data}`);

    const r3_6b = await ceremonyCategorysService.getNextSort(null);
    assert(r3_6b.success && r3_6b.data >= 3, 'TC-3.6b', `根層級下一個排序值 = ${r3_6b.data}`);

    // TC-3.7: Update
    const r3_7 = await ceremonyCategorysService.update(categoryRoot2Id, { Title: '梁皇寶懺-修改版' });
    assert(r3_7.success && r3_7.data.Title === '梁皇寶懺-修改版', 'TC-3.7', '更新類別名稱成功');

    // TC-3.8: 改回
    await ceremonyCategorysService.update(categoryRoot2Id, { Title: '梁皇寶懺' });

    // =============================================
    // Phase 4: 信眾 (Believers) CRUD
    // =============================================
    console.log('\n--- Phase 4: 信眾 CRUD ---');

    // TC-4.1: Create 基本
    believerId1 = crypto.randomUUID();
    const r4_1 = await believersService.create({
      BelieverID: believerId1,
      Name: '張三',
      HallName: '慈心堂',
      Phone: '02-12345678',
      EmployeeType: 1, // 志工
      IsFixedNumber: false,
    });
    assert(r4_1.success, 'TC-4.1', '新增信眾「張三」成功');

    // TC-4.2: Create 完整資料
    believerId2 = crypto.randomUUID();
    const r4_2 = await believersService.create({
      BelieverID: believerId2,
      Name: '李四',
      HallName: '淨心堂',
      Phone: '03-98765432',
      EmployeeType: 3, // 委員
      IsFixedNumber: true,
      MailZipcodeID: 1, // 假設 1 存在
      MailAddress: '中山路100號',
      DeadNameOne: '張父',
      LivingNameOne: '李母',
    });
    assert(r4_2.success, 'TC-4.2', '新增信眾「李四」(含地址/往生/陽上) 成功', r4_2.message);

    // TC-4.3: Create for FK test
    believerId3 = crypto.randomUUID();
    const r4_3 = await believersService.create({
      BelieverID: believerId3,
      Name: '王五',
      HallName: '善緣堂',
      Phone: '04-55566677',
      EmployeeType: 2,
      IsFixedNumber: false,
    });
    assert(r4_3.success, 'TC-4.3', '新增信眾「王五」成功');

    // TC-4.4: Search by name
    const r4_4 = await believersService.search({ keyword: '張', page: 1, pageSize: 10 });
    assert(r4_4.success && r4_4.data.rows.some(r => r.Name === '張三'), 'TC-4.4', '搜尋「張」找到張三');

    // TC-4.5: Search by phone
    const r4_5 = await believersService.search({ keyword: '03-987', page: 1, pageSize: 10 });
    assert(r4_5.success && r4_5.data.rows.some(r => r.Name === '李四'), 'TC-4.5', '搜尋電話找到李四');

    // TC-4.6: Search by hall name
    const r4_6 = await believersService.search({ keyword: '善緣', page: 1, pageSize: 10 });
    assert(r4_6.success && r4_6.data.rows.some(r => r.Name === '王五'), 'TC-4.6', '搜尋堂名找到王五');

    // TC-4.7: Lookup (autocomplete)
    const r4_7 = await believersService.lookup('張');
    assert(r4_7.success && r4_7.data.some(r => r.Name === '張三'), 'TC-4.7', 'Lookup 自動完成「張」找到張三');

    // TC-4.8: Update
    const r4_8 = await believersService.update(believerId1, { Name: '張三-修改', Phone: '02-99999999' });
    assert(r4_8.success && r4_8.data.Name === '張三-修改', 'TC-4.8', '更新信眾成功');

    // TC-4.9: Verify update
    const r4_9 = await believersService.getById(believerId1);
    assert(r4_9.success && r4_9.data.Phone === '02-99999999', 'TC-4.9', '更新持久化確認');

    // TC-4.10: Pagination
    const r4_10 = await believersService.search({ page: 1, pageSize: 2 });
    assert(r4_10.success && r4_10.data.total >= 3, 'TC-4.10', `分頁總數 ${r4_10.data?.total}，每頁 2 筆取得 ${r4_10.data?.rows.length} 筆`);

    // =============================================
    // Phase 5: 報名 (Signups) CRUD
    // =============================================
    console.log('\n--- Phase 5: 報名 CRUD ---');

    // TC-5.1: getNextNumber
    const r5_1 = await signupsService.getNextNumber(115, categoryChildId1, 1);
    assert(r5_1.success && r5_1.data === 1, 'TC-5.1', `初始號碼 = ${r5_1.data}`);

    // TC-5.2: Create signup (with believer)
    signupId1 = crypto.randomUUID();
    const r5_2 = await signupsService.create({
      SignupID: signupId1,
      Year: 115,
      CeremonyCategoryID: categoryChildId1,
      SignupType: 1,
      BelieverID: believerId1,
      Name: '張三-修改',
      Phone: '02-99999999',
      Number: 1,
      Fee: 500,
      AdminID: 1,
      Createdate: new Date(),
    });
    assert(r5_2.success, 'TC-5.2', '新增報名 (張三-修改, 大悲懺-消災) 成功', r5_2.message);

    // TC-5.3: Auto-increment number
    const r5_3 = await signupsService.getNextNumber(115, categoryChildId1, 1);
    assert(r5_3.success && r5_3.data === 2, 'TC-5.3', `下一個號碼自動遞增 = ${r5_3.data}`);

    // TC-5.4: Create signup for 王五
    signupId2 = crypto.randomUUID();
    const r5_4 = await signupsService.create({
      SignupID: signupId2,
      Year: 115,
      CeremonyCategoryID: categoryChildId1,
      SignupType: 1,
      BelieverID: believerId3,
      Name: '王五',
      Phone: '04-55566677',
      Number: 2,
      Fee: 300,
      AdminID: 1,
      Createdate: new Date(),
    });
    assert(r5_4.success, 'TC-5.4', '新增報名 (王五) 成功', r5_4.message);

    // TC-5.5: Create signup without believer (manual entry)
    signupId3 = crypto.randomUUID();
    const r5_5 = await signupsService.create({
      SignupID: signupId3,
      Year: 115,
      CeremonyCategoryID: categoryRoot2Id,
      SignupType: 1,
      BelieverID: null,
      Name: '無帳號訪客',
      Phone: '0912-345678',
      Number: 1,
      Fee: 1000,
      AdminID: 1,
      Createdate: new Date(),
    });
    assert(r5_5.success, 'TC-5.5', '手動輸入報名 (無信眾) 成功', r5_5.message);

    // TC-5.6: Search - all
    const r5_6 = await signupsService.search({ year: 115, page: 1, pageSize: 10 });
    assert(r5_6.success && r5_6.data.total >= 3, 'TC-5.6', `搜尋 115 年報名 total=${r5_6.data?.total}`);

    // TC-5.7: Search by category
    const r5_7 = await signupsService.search({ year: 115, ceremonyCategoryId: categoryChildId1 });
    assert(r5_7.success && r5_7.data.total === 2, 'TC-5.7', `篩選類別：大悲懺-消災 = ${r5_7.data?.total} 筆`);

    // TC-5.8: Search by keyword
    const r5_8 = await signupsService.search({ keyword: '訪客' });
    assert(r5_8.success && r5_8.data.rows.some(r => r.Name === '無帳號訪客'), 'TC-5.8', '關鍵字搜尋「訪客」成功');

    // TC-5.9: Get by ID
    const r5_9 = await signupsService.getById(signupId1);
    assert(r5_9.success && r5_9.data.Fee === 500, 'TC-5.9', `讀取報名 Fee=${r5_9.data?.Fee}`);

    // TC-5.10: Update
    const r5_10 = await signupsService.update(signupId1, { Fee: 600, Remark: '已修改費用' });
    assert(r5_10.success && r5_10.data.Fee === 600, 'TC-5.10', '更新報名費用 500→600 成功');

    // TC-5.11: Verify update
    const r5_11 = await signupsService.getById(signupId1);
    assert(r5_11.success && r5_11.data.Remark === '已修改費用', 'TC-5.11', '更新備註持久化確認');

    // =============================================
    // Phase 6: 操作日誌 (Signup Logs)
    // =============================================
    console.log('\n--- Phase 6: 操作日誌 ---');

    // TC-6.1: Create audit log (simulate edit save)
    logId1 = crypto.randomUUID();
    const r6_1 = await signupLogsService.create({
      SignupLogID: logId1,
      SignupID: signupId1,
      Year: 115,
      CeremonyCategoryTitle: '大悲懺-消災',
      SignupType: 1,
      Name: '張三-修改',
      Fee: 500, // old value
      Admin: '系統管理員',
      Createdate: new Date(),
    });
    assert(r6_1.success, 'TC-6.1', '建立操作日誌成功', r6_1.message);

    // TC-6.2: Search logs
    const r6_2 = await signupLogsService.search({ keyword: '張三', page: 1, pageSize: 10 });
    assert(r6_2.success && r6_2.data.rows.some(r => r.Name === '張三-修改'), 'TC-6.2', '搜尋日誌「張三」成功');

    // TC-6.3: Search by admin
    const r6_3 = await signupLogsService.search({ keyword: '系統管理員' });
    assert(r6_3.success && r6_3.data.total >= 1, 'TC-6.3', `搜尋管理者「系統管理員」= ${r6_3.data?.total} 筆`);

    // TC-6.4: Search by signupId
    const r6_4 = await signupLogsService.search({ signupId: signupId1 });
    assert(r6_4.success && r6_4.data.total >= 1, 'TC-6.4', `依 SignupID 查日誌 = ${r6_4.data?.total} 筆`);

    // =============================================
    // Phase 7: FK 約束與邊界測試
    // =============================================
    console.log('\n--- Phase 7: FK 約束與邊界測試 ---');

    // TC-7.1: 刪除有報名的信眾 (FK violation)
    const r7_1 = await believersService.remove(believerId3);
    if (!r7_1.success) {
      assert(true, 'TC-7.1', '刪除有報名的信眾被 FK 拒絕', r7_1.message?.substring(0, 80));
    } else {
      assert(false, 'TC-7.1', '刪除有報名的信眾應失敗', 'FK 約束未生效或不存在');
      // try to recreate if somehow deleted
    }

    // TC-7.2: 刪除有報名的法會類別 (FK violation)
    const r7_2 = await ceremonyCategorysService.remove(categoryChildId1);
    if (!r7_2.success) {
      assert(true, 'TC-7.2', '刪除有報名的類別被 FK 拒絕', r7_2.message?.substring(0, 80));
    } else {
      assert(false, 'TC-7.2', '刪除有報名的類別應失敗', 'FK 約束未生效');
    }

    // TC-7.3: 欄位最大長度測試 (Name NVARCHAR(30))
    const longName = '一二三四五六七八九十一二三四五六七八九十一二三四五六七八九十'; // 30 chars
    const shortBid = crypto.randomUUID();
    const r7_3 = await believersService.create({
      BelieverID: shortBid, Name: longName, EmployeeType: 1, IsFixedNumber: false,
    });
    assert(r7_3.success, 'TC-7.3', '30 字姓名建立成功');
    if (r7_3.success) await believersService.remove(shortBid);

    // TC-7.4: 超過最大長度
    const tooLong = longName + '超';
    const longBid = crypto.randomUUID();
    const r7_4 = await believersService.create({
      BelieverID: longBid, Name: tooLong, EmployeeType: 1, IsFixedNumber: false,
    });
    if (!r7_4.success) {
      assert(true, 'TC-7.4', '31 字姓名被拒絕', r7_4.message?.substring(0, 80));
    } else {
      // Check if truncated
      const check = await believersService.getById(longBid);
      await believersService.remove(longBid);
      assert(false, 'TC-7.4', '31 字姓名應被拒絕', `實際存入 ${check.data?.Name?.length} 字`);
    }

    // TC-7.5: 不存在的 ID 操作
    const r7_5 = await adminsService.getById(99999);
    assert(!r7_5.success || !r7_5.data, 'TC-7.5', '查詢不存在的 AdminID 回傳空');

    const r7_5b = await adminsService.update(99999, { Name: 'ghost' });
    assert(!r7_5b.success || !r7_5b.data, 'TC-7.5b', '更新不存在的 AdminID 回傳失敗');

    const r7_5c = await adminsService.remove(99999);
    assert(!r7_5c.success, 'TC-7.5c', '刪除不存在的 AdminID 回傳失敗');

    // TC-7.6: Delete signup (正常刪除)
    const r7_6 = await signupsService.remove(signupId3);
    assert(r7_6.success, 'TC-7.6', '刪除無帳號訪客報名成功');
    signupId3 = null;

    // =============================================
    // Cleanup
    // =============================================
    console.log('\n--- Cleanup ---');

    // Delete signups first (FK dependency)
    if (signupId1) {
      const c1 = await signupsService.remove(signupId1);
      console.log(`  Signup ${signupId1.substring(0,8)}: ${c1.success ? 'deleted' : c1.message}`);
    }
    if (signupId2) {
      const c2 = await signupsService.remove(signupId2);
      console.log(`  Signup ${signupId2.substring(0,8)}: ${c2.success ? 'deleted' : c2.message}`);
    }
    if (signupId3) {
      const c3 = await signupsService.remove(signupId3);
      console.log(`  Signup ${signupId3.substring(0,8)}: ${c3.success ? 'deleted' : c3.message}`);
    }

    // Delete log
    if (logId1) {
      const cl = await signupLogsService.remove(logId1);
      console.log(`  Log ${logId1.substring(0,8)}: ${cl.success ? 'deleted' : cl.message}`);
    }

    // Delete believers
    for (const bid of [believerId1, believerId2, believerId3]) {
      if (bid) {
        const cb = await believersService.remove(bid);
        console.log(`  Believer ${bid.substring(0,8)}: ${cb.success ? 'deleted' : cb.message}`);
      }
    }

    // Delete categories (children first)
    for (const cid of [categoryChildId1, categoryChildId2, categoryRootId, categoryRoot2Id]) {
      if (cid) {
        const cc = await ceremonyCategorysService.remove(cid);
        console.log(`  Category ${cid.substring(0,8)}: ${cc.success ? 'deleted' : cc.message}`);
      }
    }

    // Delete test admin
    if (testAdminId) {
      const ca = await adminsService.remove(testAdminId);
      console.log(`  Admin ${testAdminId}: ${ca.success ? 'deleted' : ca.message}`);
    }
    if (testAdmin2Id) {
      await adminsService.remove(testAdmin2Id);
    }

  } catch (err) {
    console.error('\n\x1b[31mUNEXPECTED ERROR:\x1b[0m', err);
  } finally {
    await closePool();
  }

  // =============================================
  // Results Summary
  // =============================================
  console.log('\n' + '='.repeat(60));
  console.log(` 測試結果: \x1b[32m${passed} PASS\x1b[0m / \x1b[31m${failed} FAIL\x1b[0m / 共 ${passed + failed} 項`);
  console.log('='.repeat(60));

  if (issues.length > 0) {
    console.log('\n\x1b[33m發現的問題:\x1b[0m');
    issues.forEach((issue, i) => {
      console.log(`  ${i + 1}. [${issue.testId}] ${issue.desc}`);
      if (issue.detail) console.log(`     => ${issue.detail}`);
    });
  }

  console.log('\n\x1b[33m已知設計問題 (需人工驗證):\x1b[0m');
  console.log('  1. Audit Log 記錄修改後的值而非修改前 (signup-edit.component.ts:158-183)');
  console.log('  2. 預付款匯入可能缺少 CeremonyCategoryID (NOT NULL 欄位)');
  console.log('  3. FK 刪除錯誤訊息為 SQL Server 原文，非使用者友善訊息');

  process.exit(failed > 0 ? 1 : 0);
}

run();
