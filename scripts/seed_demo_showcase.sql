-- ==============================================================================
-- PRAXIS - SCRIPT DE POVOAMENTO COMPLETO PARA CONTA DEMO DE VENDAS
-- Cria uma organização modelo de consultoria nutricional com uso ativo de
-- TODAS as funcionalidades da plataforma para demonstração comercial.
-- ==============================================================================
-- Credenciais de Acesso:
--   Login (Administradora / RT Principal): demo@praxisnutri.com
--   Login (Nutricionista de Campo):        Jamily@praxisnutri.com
--   Senha para ambos:                     Praxis@123
-- ==============================================================================

DO $$
DECLARE
    -- IDs de Entidades Principais
    v_tenant_id uuid := gen_random_uuid();
    v_plan_id uuid;
    v_user_admin_id uuid := gen_random_uuid();
    v_user_nutri_id uuid := gen_random_uuid();
    v_nutri1_id uuid := gen_random_uuid();
    v_nutri2_id uuid := gen_random_uuid();

    -- Clientes e Unidades
    v_client1_id uuid := gen_random_uuid();
    v_client2_id uuid := gen_random_uuid();
    v_client3_id uuid := gen_random_uuid();
    v_unit1_id uuid := gen_random_uuid();
    v_unit2_id uuid := gen_random_uuid();
    v_unit3_id uuid := gen_random_uuid();
    v_unit4_id uuid := gen_random_uuid();

    -- ARTs e Checklists
    v_art1_id uuid := gen_random_uuid();
    v_art2_id uuid := gen_random_uuid();
    v_checklist_id uuid := gen_random_uuid();
    v_chk_item1 uuid := gen_random_uuid();
    v_chk_item2 uuid := gen_random_uuid();
    v_chk_item3 uuid := gen_random_uuid();
    v_chk_item4 uuid := gen_random_uuid();
    v_chk_item5 uuid := gen_random_uuid();
    v_chk_item6 uuid := gen_random_uuid();
    v_chk_item7 uuid := gen_random_uuid();

    -- Visitas e Não-Conformidades
    v_visit1_id uuid := gen_random_uuid();
    v_visit2_id uuid := gen_random_uuid();
    v_vitem1 uuid := gen_random_uuid();
    v_vitem2 uuid := gen_random_uuid();
    v_vitem3 uuid := gen_random_uuid();
    v_vitem4 uuid := gen_random_uuid();
    v_vitem5 uuid := gen_random_uuid();
    v_vitem6 uuid := gen_random_uuid();
    v_vitem7 uuid := gen_random_uuid();
    v_nc1_id uuid := gen_random_uuid();
    v_nc2_id uuid := gen_random_uuid();
    v_action1_id uuid := gen_random_uuid();
    v_action2_id uuid := gen_random_uuid();

    -- Etiquetagem e Validade
    v_rule1_id uuid := gen_random_uuid();
    v_rule2_id uuid := gen_random_uuid();
    v_rule3_id uuid := gen_random_uuid();
    v_prod1_id uuid := gen_random_uuid();
    v_prod2_id uuid := gen_random_uuid();
    v_prod3_id uuid := gen_random_uuid();
    v_batch1_id uuid := gen_random_uuid();
    v_batch2_id uuid := gen_random_uuid();
    v_label1_id uuid := gen_random_uuid();
    v_label2_id uuid := gen_random_uuid();
    v_label3_id uuid := gen_random_uuid();

BEGIN
    -- 1. Obter ID do Plano 'professional' (ou 'enterprise' ou o primeiro ativo)
    SELECT "Id" INTO v_plan_id FROM "Plans" WHERE "Code" = 'professional' LIMIT 1;
    IF v_plan_id IS NULL THEN
        SELECT "Id" INTO v_plan_id FROM "Plans" WHERE "IsActive" = TRUE LIMIT 1;
    END IF;

    -- 2. Limpeza prévia para garantir reentrabilidade segura
    DELETE FROM "FoodLabels" WHERE "PublicToken" IN ('praxis-demo-molho-tomate', 'praxis-demo-file-frango', 'praxis-demo-mix-salada');
    DELETE FROM "FoodLabels" WHERE "TenantId" IN (SELECT "Id" FROM "Tenants" WHERE "Cnpj" = '45.892.147/0001-39');
    DELETE FROM "ProductBatches" WHERE "TenantId" IN (SELECT "Id" FROM "Tenants" WHERE "Cnpj" = '45.892.147/0001-39');
    DELETE FROM "Products" WHERE "TenantId" IN (SELECT "Id" FROM "Tenants" WHERE "Cnpj" = '45.892.147/0001-39');
    DELETE FROM "ValidityRules" WHERE "TenantId" IN (SELECT "Id" FROM "Tenants" WHERE "Cnpj" = '45.892.147/0001-39');
    DELETE FROM "ActionItems" WHERE "TenantId" IN (SELECT "Id" FROM "Tenants" WHERE "Cnpj" = '45.892.147/0001-39');
    DELETE FROM "NonConformities" WHERE "TenantId" IN (SELECT "Id" FROM "Tenants" WHERE "Cnpj" = '45.892.147/0001-39');
    DELETE FROM "VisitItems" WHERE "VisitId" IN (SELECT "Id" FROM "Visits" WHERE "TenantId" IN (SELECT "Id" FROM "Tenants" WHERE "Cnpj" = '45.892.147/0001-39'));
    DELETE FROM "Visits" WHERE "TenantId" IN (SELECT "Id" FROM "Tenants" WHERE "Cnpj" = '45.892.147/0001-39');
    DELETE FROM "ChecklistItems" WHERE "ChecklistId" IN (SELECT "Id" FROM "Checklists" WHERE "TenantId" IN (SELECT "Id" FROM "Tenants" WHERE "Cnpj" = '45.892.147/0001-39'));
    DELETE FROM "Checklists" WHERE "TenantId" IN (SELECT "Id" FROM "Tenants" WHERE "Cnpj" = '45.892.147/0001-39');
    DELETE FROM "ARTs" WHERE "TenantId" IN (SELECT "Id" FROM "Tenants" WHERE "Cnpj" = '45.892.147/0001-39');
    DELETE FROM "NutritionistUnitAssignments" WHERE "TenantId" IN (SELECT "Id" FROM "Tenants" WHERE "Cnpj" = '45.892.147/0001-39');
    DELETE FROM "Units" WHERE "TenantId" IN (SELECT "Id" FROM "Tenants" WHERE "Cnpj" = '45.892.147/0001-39');
    DELETE FROM "ClientCompanies" WHERE "TenantId" IN (SELECT "Id" FROM "Tenants" WHERE "Cnpj" = '45.892.147/0001-39');
    DELETE FROM "Nutritionists" WHERE "TenantId" IN (SELECT "Id" FROM "Tenants" WHERE "Cnpj" = '45.892.147/0001-39');
    DELETE FROM "Subscriptions" WHERE "TenantId" IN (SELECT "Id" FROM "Tenants" WHERE "Cnpj" = '45.892.147/0001-39');
    DELETE FROM "AuditLogs" WHERE "TenantId" IN (SELECT "Id" FROM "Tenants" WHERE "Cnpj" = '45.892.147/0001-39');
    DELETE FROM "Users" WHERE "Email" IN ('demo@praxisnutri.com', 'Jamily@praxisnutri.com');
    DELETE FROM "Tenants" WHERE "Cnpj" = '45.892.147/0001-39' OR "Email" = 'contato@nutriquali.com.br';

    -- 3. Empresa de Consultoria Modelo (Tenant)
    INSERT INTO "Tenants" (
        "Id", "Name", "LegalName", "Cnpj", "Email", "Phone", "Status", "IsDeleted", "CreatedAt", "UpdatedAt"
    ) VALUES (
        v_tenant_id,
        'NutriQuali Consultoria & Gestão Nutricional',
        'NutriQuali Consultoria e Serviços Nutricionais Ltda',
        '45.892.147/0001-39',
        'contato@nutriquali.com.br',
        '(11) 3289-4500',
        1, -- Active
        FALSE,
        NOW() - INTERVAL '60 days',
        NOW()
    );

    -- 4. Assinatura Comercial Ativa (Plano Professional)
    IF v_plan_id IS NOT NULL THEN
        INSERT INTO "Subscriptions" (
            "Id", "TenantId", "PlanId", "Status", "BillingCycle", "StartedAt",
            "CurrentPeriodStart", "CurrentPeriodEnd", "PaymentProvider", "EndsAtPeriodEnd", "CreatedAt", "UpdatedAt"
        ) VALUES (
            gen_random_uuid(),
            v_tenant_id,
            v_plan_id,
            2, -- Active
            1, -- Monthly
            NOW() - INTERVAL '60 days',
            NOW() - INTERVAL '5 days',
            NOW() + INTERVAL '25 days',
            'Asaas',
            FALSE,
            NOW() - INTERVAL '60 days',
            NOW()
        );
    END IF;

    -- 5. Usuários e Equipe Técnica (Senha: Praxis@123)
    -- Administradora / RT Responsável
    INSERT INTO "Users" (
        "Id", "TenantId", "Name", "Email", "PasswordHash", "Role", "Status",
        "DateOfBirth", "IsDeleted", "CreatedAt", "UpdatedAt"
    ) VALUES (
        v_user_admin_id,
        v_tenant_id,
        'Dra. Zenilde Vasconcelos',
        'demo@praxisnutri.com',
        '$2a$11$MWMw5tzgvoQSsEyQTHhKEeuIKmZnbz8eAP.rEKO6mzyPRJctYw6fK',
        2, -- TenantAdmin
        1, -- Active
        '1988-06-15',
        FALSE,
        NOW() - INTERVAL '60 days',
        NOW()
    );

    -- Nutricionista de Campo / Auditora
    INSERT INTO "Users" (
        "Id", "TenantId", "Name", "Email", "PasswordHash", "Role", "Status",
        "DateOfBirth", "IsDeleted", "CreatedAt", "UpdatedAt"
    ) VALUES (
        v_user_nutri_id,
        v_tenant_id,
        'Dra. Jamily Mendes',
        'Jamily@praxisnutri.com',
        '$2a$11$MWMw5tzgvoQSsEyQTHhKEeuIKmZnbz8eAP.rEKO6mzyPRJctYw6fK',
        3, -- Nutritionist
        1, -- Active
        '1993-11-20',
        FALSE,
        NOW() - INTERVAL '45 days',
        NOW()
    );

    -- Perfis Profissionais (CRN)
    INSERT INTO "Nutritionists" (
        "Id", "TenantId", "UserId", "Crn", "Phone", "Status", "IsDeleted", "CreatedAt", "UpdatedAt"
    ) VALUES 
    (v_nutri1_id, v_tenant_id, v_user_admin_id, 'CRN-3 48291', '(11) 99123-4567', 1, FALSE, NOW() - INTERVAL '60 days', NOW()),
    (v_nutri2_id, v_tenant_id, v_user_nutri_id, 'CRN-3 56104', '(11) 98234-5678', 1, FALSE, NOW() - INTERVAL '45 days', NOW());

    -- 6. Clientes Contratantes (Empresas de Alimentação)
    INSERT INTO "ClientCompanies" (
        "Id", "TenantId", "LegalName", "TradeName", "Cnpj", "Email", "Phone",
        "Address", "ResponsibleName", "Notes", "Status", "IsDeleted", "CreatedAt", "UpdatedAt"
    ) VALUES 
    (
        v_client1_id,
        v_tenant_id,
        'Bistrô Sabor Gastronomia Ltda',
        'Restaurante Bistrô & Sabor Contemporâneo',
        '12.345.678/0001-90',
        'gerencia@bistrosabor.com.br',
        '(11) 3045-8899',
        'Alameda Santos, 1200 - Cerqueira César, São Paulo - SP',
        'Carlos Eduardo Silveira (Proprietário)',
        'Restaurante comercial à la carte e buffet. Produção média de 450 refeições/dia.',
        1, FALSE, NOW() - INTERVAL '50 days', NOW()
    ),
    (
        v_client2_id,
        v_tenant_id,
        'Bella Vista Panificação Artesanal Eireli',
        'Padaria & Confeitaria Bella Vista',
        '98.765.432/0001-11',
        'contato@bellavistapadaria.com.br',
        '(11) 3256-1122',
        'Rua da Consolação, 890 - Consolação, São Paulo - SP',
        'Fernanda Rossi (Gerente Geral)',
        'Panificação de fermentação natural, confeitaria fina e café colonial.',
        1, FALSE, NOW() - INTERVAL '40 days', NOW()
    ),
    (
        v_client3_id,
        v_tenant_id,
        'Instituição Educacional Pequenos Passos Ltda',
        'Colégio Pequenos Passos (Cantina & Refeitório)',
        '34.567.890/0001-22',
        'nutricao@pequenospassos.com.br',
        '(11) 3881-2020',
        'Av. Brigadeiro Luís Antônio, 3400 - Jardim Paulista, São Paulo - SP',
        'Helena Castro (Coordenadora de Nutrição Escolar)',
        'Alimentação escolar para 600 alunos com protocolo rígido para celíacos e alérgenos.',
        1, FALSE, NOW() - INTERVAL '30 days', NOW()
    );

    -- 7. Unidades Operacionais
    INSERT INTO "Units" (
        "Id", "TenantId", "ClientCompanyId", "Name", "Address", "Phone",
        "ResponsibleName", "Notes", "Status", "IsDeleted", "CreatedAt", "UpdatedAt"
    ) VALUES 
    (
        v_unit1_id,
        v_tenant_id,
        v_client1_id,
        'Bistrô Sabor - Unidade Jardins (Matriz)',
        'Alameda Santos, 1200 - Jardins, São Paulo - SP',
        '(11) 3045-8899',
        'Chef Rodrigo Toledo',
        'Cozinha quente, sushibar e confeitaria.',
        1, FALSE, NOW() - INTERVAL '50 days', NOW()
    ),
    (
        v_unit2_id,
        v_tenant_id,
        v_client1_id,
        'Bistrô Sabor - Unidade Itaim Bibi',
        'Rua Joaquim Floriano, 450 - Itaim Bibi, São Paulo - SP',
        '(11) 3168-5544',
        'Sous-chef Marcelo Neves',
        'Operação de salão e delivery expresso.',
        1, FALSE, NOW() - INTERVAL '50 days', NOW()
    ),
    (
        v_unit3_id,
        v_tenant_id,
        v_client2_id,
        'Padaria Bella Vista - Matriz Centro',
        'Rua da Consolação, 890 - Centro, São Paulo - SP',
        '(11) 3256-1122',
        'Antônio Lima (Mestre Padeiro)',
        'Área de manipulação pesada de massas e confeitaria.',
        1, FALSE, NOW() - INTERVAL '40 days', NOW()
    ),
    (
        v_unit4_id,
        v_tenant_id,
        v_client3_id,
        'Colégio Pequenos Passos - Cozinha & Refeitório Central',
        'Av. Brigadeiro Luís Antônio, 3400 - São Paulo - SP',
        '(11) 3881-2020',
        'Lúcia Andrade (Chefe de Refeitório)',
        'Cozinha escolar com atendimento diário nos turnos manhã e tarde.',
        1, FALSE, NOW() - INTERVAL '30 days', NOW()
    );

    -- 8. Atribuição de Unidades às Nutricionistas
    INSERT INTO "NutritionistUnitAssignments" ("Id", "TenantId", "NutritionistId", "UnitId", "CreatedAt", "UpdatedAt")
    VALUES 
    (gen_random_uuid(), v_tenant_id, v_nutri1_id, v_unit1_id, NOW(), NOW()),
    (gen_random_uuid(), v_tenant_id, v_nutri1_id, v_unit2_id, NOW(), NOW()),
    (gen_random_uuid(), v_tenant_id, v_nutri2_id, v_unit3_id, NOW(), NOW()),
    (gen_random_uuid(), v_tenant_id, v_nutri2_id, v_unit4_id, NOW(), NOW());

    -- 9. Anotações de Responsabilidade Técnica (ARTs)
    INSERT INTO "ARTs" (
        "Id", "TenantId", "UnitId", "NutritionistId", "Number", "StartDate",
        "EndDate", "Status", "DocumentUrl", "Notes", "IsDeleted", "CreatedAt", "UpdatedAt"
    ) VALUES 
    (
        v_art1_id,
        v_tenant_id,
        v_unit1_id,
        v_nutri1_id,
        'SP-2026-0048291',
        '2026-01-01 00:00:00+00',
        '2026-12-31 23:59:59+00',
        1, -- Active
        'https://crn3.org.br/certidoes/validacao?art=SP-2026-0048291',
        'Responsabilidade Técnica integral perante o CRN-3 para restaurante comercial com 35 colaboradores.',
        FALSE, NOW() - INTERVAL '50 days', NOW()
    ),
    (
        v_art2_id,
        v_tenant_id,
        v_unit3_id,
        v_nutri2_id,
        'SP-2026-0056104',
        '2026-02-15 00:00:00+00',
        '2027-02-14 23:59:59+00',
        1, -- Active
        'https://crn3.org.br/certidoes/validacao?art=SP-2026-0056104',
        'Responsabilidade Técnica para indústria de panificação e confeitaria fina com rotulagem nutricional.',
        FALSE, NOW() - INTERVAL '40 days', NOW()
    );

    -- 10. Checklist de Boas Práticas (RDC 216 / ANVISA)
    INSERT INTO "Checklists" (
        "Id", "TenantId", "Name", "Description", "Status", "IsDeleted", "CreatedAt", "UpdatedAt"
    ) VALUES (
        v_checklist_id,
        v_tenant_id,
        'Auditoria RDC 216/ANVISA - Boas Práticas em Serviços de Alimentação',
        'Checklist padrão oficial para vistorias higiênico-sanitárias, manipulação e prevenção de contaminação cruzada.',
        1, FALSE, NOW() - INTERVAL '60 days', NOW()
    );

    -- Itens do Checklist
    INSERT INTO "ChecklistItems" (
        "Id", "ChecklistId", "Category", "Description", "Order", "Required", "Status", "IsDeleted", "CreatedAt", "UpdatedAt"
    ) VALUES 
    (v_chk_item1, v_checklist_id, 'Higiene Pessoal', 'Manipuladores com uniformes limpos, sem adornos (brincos, anéis, relógios) e cabelos totalmente protegidos por touca descartável.', 1, TRUE, 1, FALSE, NOW(), NOW()),
    (v_chk_item2, v_checklist_id, 'Higiene Pessoal', 'Lavatório exclusivo para as mãos provido de sabonete bactericida, papel toalha não reciclado e lixeira acionada sem contato manual.', 2, TRUE, 1, FALSE, NOW(), NOW()),
    (v_chk_item3, v_checklist_id, 'Armazenamento', 'Alimentos armazenados identificados com etiquetas legíveis contendo nome, lote, data de abertura/preparo e prazo de validade.', 3, TRUE, 1, FALSE, NOW(), NOW()),
    (v_chk_item4, v_checklist_id, 'Controle de Temperatura', 'Registro diário e controle das temperaturas de refrigeradores (0°C a 8°C) e freezers (≤ -18°C) documentados em planilha.', 4, TRUE, 1, FALSE, NOW(), NOW()),
    (v_chk_item5, v_checklist_id, 'Instalações & Equipamentos', 'Bancadas de inox, tábuas de corte e equipamentos devidamente sanitizados e livres de resíduos incrustados.', 5, TRUE, 1, FALSE, NOW(), NOW()),
    (v_chk_item6, v_checklist_id, 'Controle de Pragas', 'Telas milimetradas intactas nas janelas e ralos com sistema escamoteável ou tampa mantidos fechados.', 6, TRUE, 1, FALSE, NOW(), NOW()),
    (v_chk_item7, v_checklist_id, 'Manipulação Segura', 'Separação rígida de utensílios (tábuas coloridas) evitando cruzamento de alimentos crus com preparações prontas.', 7, TRUE, 1, FALSE, NOW(), NOW());

    -- 11. Visita Técnica 1: CONCLUÍDA (Gera relatório e pontuação no Bistrô Jardins)
    INSERT INTO "Visits" (
        "Id", "TenantId", "UnitId", "NutritionistId", "ChecklistId",
        "ScheduledAt", "StartedAt", "FinishedAt", "Status", "Notes", "IsDeleted", "CreatedAt", "UpdatedAt"
    ) VALUES (
        v_visit1_id,
        v_tenant_id,
        v_unit1_id,
        v_nutri1_id,
        v_checklist_id,
        NOW() - INTERVAL '3 days',
        (NOW() - INTERVAL '3 days') + INTERVAL '9 hours',
        (NOW() - INTERVAL '3 days') + INTERVAL '11 hours 30 minutes',
        3, -- Finished
        'Auditoria técnica mensal concluída com sucesso. Índice geral de conformidade atingiu 71.4%. Foram detectadas 2 não-conformidades de atenção que demandam plano de ação 5W2H.',
        FALSE,
        NOW() - INTERVAL '3 days',
        NOW() - INTERVAL '3 days'
    );

    -- Avaliação dos Itens da Visita 1
    INSERT INTO "VisitItems" ("Id", "VisitId", "ChecklistItemId", "Result", "Observation", "CreatedAt", "UpdatedAt")
    VALUES 
    (v_vitem1, v_visit1_id, v_chk_item1, 1, 'Equipe completa uniformizada e barbeada. Excelente padrão.', NOW() - INTERVAL '3 days', NOW()),
    (v_vitem2, v_visit1_id, v_chk_item2, 1, 'Pias higienizadas, saboneteira e toalheiro abastecidos.', NOW() - INTERVAL '3 days', NOW()),
    (v_vitem3, v_visit1_id, v_chk_item3, 1, 'Etiquetas impressas pelo sistema PRAXIS em todas as cubas e bisnagas.', NOW() - INTERVAL '3 days', NOW()),
    (v_vitem4, v_visit1_id, v_chk_item4, 2, 'Refrigerador de apoio da cozinha quente marcando 11.8°C no termômetro.', NOW() - INTERVAL '3 days', NOW()), -- NÃO CONFORME
    (v_vitem5, v_visit1_id, v_chk_item5, 1, 'Bancadas sanitizadas com álcool 70% entre as preparações.', NOW() - INTERVAL '3 days', NOW()),
    (v_vitem6, v_visit1_id, v_chk_item6, 2, 'Tela de proteção da área de lavagem com pequeno rasgo lateral de 5cm.', NOW() - INTERVAL '3 days', NOW()), -- NÃO CONFORME
    (v_vitem7, v_visit1_id, v_chk_item7, 1, 'Tábuas branca, verde e vermelha sendo utilizadas corretamente.', NOW() - INTERVAL '3 days', NOW());

    -- Visita Técnica 2: AGENDADA (Para mostrar calendário e próximas visitas no dashboard)
    INSERT INTO "Visits" (
        "Id", "TenantId", "UnitId", "NutritionistId", "ChecklistId",
        "ScheduledAt", "Status", "Notes", "IsDeleted", "CreatedAt", "UpdatedAt"
    ) VALUES (
        v_visit2_id,
        v_tenant_id,
        v_unit3_id,
        v_nutri2_id,
        v_checklist_id,
        NOW() + INTERVAL '2 days' + INTERVAL '14 hours',
        1, -- Scheduled
        'Vistoria quinzenal com foco em etiquetagem de validades e auditoria das matérias-primas da confeitaria.',
        FALSE,
        NOW() - INTERVAL '2 days',
        NOW()
    );

    -- 12. Não-Conformidades Geradas na Visita 1
    -- NC 1 (Temperatura Alta no Refrigerador)
    INSERT INTO "NonConformities" (
        "Id", "TenantId", "VisitId", "VisitItemId", "Category", "Description",
        "Severity", "Status", "DueDate", "CorrectiveAction", "IsDeleted", "CreatedAt", "UpdatedAt"
    ) VALUES (
        v_nc1_id,
        v_tenant_id,
        v_visit1_id,
        v_vitem4,
        'Controle de Temperatura',
        'Refrigerador de apoio da cozinha quente operando a 11.8°C (limite máximo permitido é 8°C). Risco de proliferação microbiana em molhos e frios prontos.',
        3, -- Alta
        2, -- EmAndamento
        NOW() + INTERVAL '2 days',
        'Transferir imediatamente os alimentos para a câmara fria principal e acionar assistência de refrigeração.',
        FALSE,
        NOW() - INTERVAL '3 days',
        NOW()
    );

    -- NC 2 (Tela com avaria)
    INSERT INTO "NonConformities" (
        "Id", "TenantId", "VisitId", "VisitItemId", "Category", "Description",
        "Severity", "Status", "DueDate", "CorrectiveAction", "IsDeleted", "CreatedAt", "UpdatedAt"
    ) VALUES (
        v_nc2_id,
        v_tenant_id,
        v_visit1_id,
        v_vitem6,
        'Controle de Pragas & Instalações',
        'Tela milimetrada da janela lateral da área de lavagem de louças apresentava rasgo de aprox. 5cm.',
        2, -- Media
        1, -- Aberta
        NOW() + INTERVAL '5 days',
        'Realizar troca imediata da malha de proteção para impedir o acesso de insetos voadores.',
        FALSE,
        NOW() - INTERVAL '3 days',
        NOW()
    );

    -- 13. Plano de Ação 5W2H (Ações Corretivas)
    -- Ação 1 (Em Andamento com orçamento)
    INSERT INTO "ActionItems" (
        "Id", "TenantId", "NonConformityId", "What", "Description", "Why",
        "ResponsibleUserId", "ResponsibleName", "DueDate", "Where", "How", "HowMuch",
        "Priority", "Status", "StartedAt", "IsDeleted", "CreatedAt", "UpdatedAt"
    ) VALUES (
        v_action1_id,
        v_tenant_id,
        v_nc1_id,
        'Manutenção corretiva e recarga de gás do refrigerador de apoio',
        'Manutenção corretiva e recarga de gás do refrigerador de apoio',
        'Garantir a conservação dos alimentos abaixo de 8°C e atender a RDC 216/ANVISA',
        v_user_admin_id,
        'Carlos Eduardo (Gerente) / Refrigeração FrioMax',
        NOW() + INTERVAL '2 days',
        'Cozinha Quente - Linha de Finalização',
        'Substituição da borracha de vedação magnética e regulagem do termostato digital.',
        380.00,
        3, -- Alta
        2, -- EmAndamento
        NOW() - INTERVAL '1 day',
        FALSE,
        NOW() - INTERVAL '3 days',
        NOW()
    );

    -- Ação 2 (Pendente)
    INSERT INTO "ActionItems" (
        "Id", "TenantId", "NonConformityId", "What", "Description", "Why",
        "ResponsibleUserId", "ResponsibleName", "DueDate", "Where", "How", "HowMuch",
        "Priority", "Status", "IsDeleted", "CreatedAt", "UpdatedAt"
    ) VALUES (
        v_action2_id,
        v_tenant_id,
        v_nc2_id,
        'Instalação de nova tela milimetrada na área de higienização',
        'Instalação de nova tela milimetrada na área de higienização',
        'Bloquear entrada de moscas e pragas atraídas pelo vapor da máquina de lavar louças',
        v_user_nutri_id,
        'Marcio (Manutenção Predial do Restaurante)',
        NOW() + INTERVAL '5 days',
        'Área de Lavagem de Utensílios e Louças',
        'Remover a moldura antiga, instalar tela de alumínio milimetrada lavável e selar com silicone.',
        95.00,
        2, -- Media
        1, -- Pendente
        FALSE,
        NOW() - INTERVAL '3 days',
        NOW()
    );

    -- 14. Regras de Validade Técnicas (RDC 216 / ANVISA)
    INSERT INTO "ValidityRules" (
        "Id", "TenantId", "UnitId", "Name", "Description", "ProductCategory",
        "LabelType", "OperationType", "StorageCondition", "MaximumTemperature",
        "ValidityValue", "ValidityUnit", "AllowManualExpiration", "RequiresTechnicalBasis",
        "RegulatoryReference", "IsActive", "IsDeleted", "CreatedAt", "UpdatedAt"
    ) VALUES 
    (
        v_rule1_id,
        v_tenant_id,
        v_unit1_id,
        'Molhos e Bases Culinárias Cozidas',
        'Preparações cozidas mantidas sob refrigeração de até 4°C.',
        'Molhos e Bases',
        1, -- PreparedFood
        1, -- Preparation
        2, -- Refrigerated
        4.0,
        72, -- 72 horas
        1,  -- Hours
        TRUE, FALSE,
        'RDC 216/2004 item 4.7.4',
        TRUE, FALSE, NOW() - INTERVAL '50 days', NOW()
    ),
    (
        v_rule2_id,
        v_tenant_id,
        v_unit1_id,
        'Carnes e Aves Porcionadas / Marinadas',
        'Carnes manipuladas e fracionadas mantidas em recipientes herméticos.',
        'Carnes e Proteínas',
        3, -- PortionedProduct
        3, -- Portioning
        2, -- Refrigerated
        4.0,
        48, -- 48 horas
        1,  -- Hours
        TRUE, FALSE,
        'Portaria CVS 5/2013 Art. 52',
        TRUE, FALSE, NOW() - INTERVAL '50 days', NOW()
    ),
    (
        v_rule3_id,
        v_tenant_id,
        v_unit1_id,
        'Hortaliças e Vegetais Sanitizados',
        'Vegetais higienizados com solução clorada a 200ppm e centrifugados.',
        'Hortifrúti',
        4, -- PrePreparation
        5, -- PrePreparation
        2, -- Refrigerated
        8.0,
        5,  -- 5 dias
        2,  -- Days
        TRUE, FALSE,
        'RDC 216/2004 item 4.8.4',
        TRUE, FALSE, NOW() - INTERVAL '50 days', NOW()
    );

    -- 15. Produtos do Cardápio / Produção
    INSERT INTO "Products" (
        "Id", "TenantId", "UnitId", "Name", "Description", "Category",
        "IsActive", "IsDeleted", "CreatedAt", "UpdatedAt"
    ) VALUES 
    (
        v_prod1_id,
        v_tenant_id,
        v_unit1_id,
        'Molho de Tomate Rústico Artesanal',
        'Molho de tomate pelado italiano com manjericão fresco e azeite extravirgem.',
        'Molhos e Bases',
        TRUE, FALSE, NOW() - INTERVAL '50 days', NOW()
    ),
    (
        v_prod2_id,
        v_tenant_id,
        v_unit1_id,
        'Filé de Frango Porcionado (150g)',
        'Peito de frango limpo, porcionado e marinado com ervas finas.',
        'Carnes e Proteínas',
        TRUE, FALSE, NOW() - INTERVAL '50 days', NOW()
    ),
    (
        v_prod3_id,
        v_tenant_id,
        v_unit1_id,
        'Mix de Folhas Verdes Sanitizadas',
        'Alface americana, crespa e rúcula hidropônica lavadas e prontas para servir.',
        'Hortifrúti',
        TRUE, FALSE, NOW() - INTERVAL '50 days', NOW()
    );

    -- Lotes dos Produtos
    INSERT INTO "ProductBatches" (
        "Id", "TenantId", "ProductId", "BatchCode", "OriginalBatchCode",
        "ManufacturingDate", "OriginalExpirationDate", "IsDeleted", "CreatedAt", "UpdatedAt"
    ) VALUES 
    (
        v_batch1_id,
        v_tenant_id,
        v_prod1_id,
        'LOTE-TOM-2601',
        'IND-78945',
        NOW() - INTERVAL '2 days',
        NOW() + INTERVAL '180 days',
        FALSE, NOW() - INTERVAL '2 days', NOW()
    ),
    (
        v_batch2_id,
        v_tenant_id,
        v_prod2_id,
        'LOTE-FRG-3312',
        'SIF-BR-9902',
        NOW() - INTERVAL '1 day',
        NOW() + INTERVAL '12 days',
        FALSE, NOW() - INTERVAL '1 day', NOW()
    );

    -- 16. Etiquetas Inteligentes Geradas (com Token Público para leitura de QR Code)
    -- Etiqueta 1: Molho de Tomate (Válida por 72h)
    INSERT INTO "FoodLabels" (
        "Id", "TenantId", "UnitId", "ProductId", "ProductBatchId", "ValidityRuleId",
        "LabelType", "OperationType", "Status", "Description", "InternalBatchCode",
        "ManufacturedAt", "PreparedAt", "ValidityStartAt", "CalculatedExpirationDate",
        "ValiditySource", "StorageCondition", "StorageTemperatureMax", "StorageInstructions",
        "PublicToken", "PrintCount", "LastPrintedAt", "CreatedByUserId",
        "IsDeleted", "CreatedAt", "UpdatedAt"
    ) VALUES (
        v_label1_id,
        v_tenant_id,
        v_unit1_id,
        v_prod1_id,
        v_batch1_id,
        v_rule1_id,
        1, -- PreparedFood
        1, -- Preparation
        1, -- Active
        'Molho de Tomate Rústico Artesanal - Panela 1',
        'LOTE-TOM-2601',
        NOW() - INTERVAL '4 hours',
        NOW() - INTERVAL '4 hours',
        NOW() - INTERVAL '4 hours',
        (NOW() - INTERVAL '4 hours') + INTERVAL '72 hours',
        1, -- Rule
        2, -- Refrigerated
        4.0,
        'Manter sob refrigeração de 0°C a 4°C em recipiente vedado.',
        'praxis-demo-molho-tomate',
        4,
        NOW() - INTERVAL '3 hours',
        v_user_admin_id,
        FALSE,
        NOW() - INTERVAL '4 hours',
        NOW()
    );

    -- Etiqueta 2: Filé de Frango Porcionado (Válida por 48h)
    INSERT INTO "FoodLabels" (
        "Id", "TenantId", "UnitId", "ProductId", "ProductBatchId", "ValidityRuleId",
        "LabelType", "OperationType", "Status", "Description", "InternalBatchCode",
        "OpenedAt", "PortionedAt", "ValidityStartAt", "CalculatedExpirationDate",
        "ValiditySource", "StorageCondition", "StorageTemperatureMax", "StorageInstructions",
        "PublicToken", "PrintCount", "LastPrintedAt", "CreatedByUserId",
        "IsDeleted", "CreatedAt", "UpdatedAt"
    ) VALUES (
        v_label2_id,
        v_tenant_id,
        v_unit1_id,
        v_prod2_id,
        v_batch2_id,
        v_rule2_id,
        3, -- PortionedProduct
        3, -- Portioning
        1, -- Active
        'Filé de Frango Porcionado 150g - Caixa A',
        'LOTE-FRG-3312',
        NOW() - INTERVAL '8 hours',
        NOW() - INTERVAL '8 hours',
        NOW() - INTERVAL '8 hours',
        (NOW() - INTERVAL '8 hours') + INTERVAL '48 hours',
        1, -- Rule
        2, -- Refrigerated
        4.0,
        'Consumir no prazo ou congelar a -18°C antes do término.',
        'praxis-demo-file-frango',
        8,
        NOW() - INTERVAL '7 hours',
        v_user_admin_id,
        FALSE,
        NOW() - INTERVAL '8 hours',
        NOW()
    );

    -- Etiqueta 3: Mix de Salada Sanitizada (Válida por 5 dias)
    INSERT INTO "FoodLabels" (
        "Id", "TenantId", "UnitId", "ProductId", "ValidityRuleId",
        "LabelType", "OperationType", "Status", "Description", "InternalBatchCode",
        "PreparedAt", "ValidityStartAt", "CalculatedExpirationDate",
        "ValiditySource", "StorageCondition", "StorageTemperatureMax", "StorageInstructions",
        "PublicToken", "PrintCount", "LastPrintedAt", "CreatedByUserId",
        "IsDeleted", "CreatedAt", "UpdatedAt"
    ) VALUES (
        v_label3_id,
        v_tenant_id,
        v_unit1_id,
        v_prod3_id,
        v_rule3_id,
        4, -- PrePreparation
        5, -- PrePreparation
        1, -- Active
        'Mix de Folhas Higienizadas (Alface + Rúcula)',
        'LOTE-MIX-009',
        NOW() - INTERVAL '1 day',
        NOW() - INTERVAL '1 day',
        (NOW() - INTERVAL '1 day') + INTERVAL '5 days',
        1, -- Rule
        2, -- Refrigerated
        8.0,
        'Armazenar em monobloco plástico higienizado com tampa.',
        'praxis-demo-mix-salada',
        2,
        NOW() - INTERVAL '1 day',
        v_user_admin_id,
        FALSE,
        NOW() - INTERVAL '1 day',
        NOW()
    );

    -- 17. Registro de Auditoria Inicial (AuditLogs)
    INSERT INTO "AuditLogs" (
        "Id", "TenantId", "UserId", "Action", "Entity", "EntityId", "Metadata", "CreatedAt", "UpdatedAt"
    ) VALUES 
    (gen_random_uuid(), v_tenant_id, v_user_admin_id, 'DEMO_SEED', 'Tenant', v_tenant_id::text, 'Povoamento demonstrativo executado com sucesso.', NOW(), NOW()),
    (gen_random_uuid(), v_tenant_id, v_user_admin_id, 'LOGIN', 'User', v_user_admin_id::text, 'Sessão demonstrativa configurada.', NOW(), NOW());

    RAISE NOTICE 'Conta DEMO povoada com sucesso! Login: demo@praxisnutri.com | Senha: Praxis@123';
END $$;
