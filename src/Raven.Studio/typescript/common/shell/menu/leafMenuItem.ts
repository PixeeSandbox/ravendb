﻿/// <reference path="../../../../typings/tsd.d.ts" />
import generalUtils = require("common/generalUtils");
import intermediateMenuItem = require("common/shell/menu/intermediateMenuItem");

class leafMenuItem implements menuItem {
    title: string;
    tooltip: string;
    nav: boolean | KnockoutObservable<boolean>;
    route: string | Array<string>;
    moduleId: any;
    hash: string;
    dynamicHash: dynamicHashType;
    css: string;
    openAsDialog: boolean;
    path: KnockoutComputed<string>;
    parent: KnockoutObservable<intermediateMenuItem> = ko.observable(null);
    disableWithReason?: KnockoutObservable<string>;
    shardingMode: shardingMode;
    type: menuItemType = "leaf";
    itemRouteToHighlight: string;
    alias: boolean;
    requiredAccess: accessLevel;
    
    badgeHtml: string;
    badgeData: KnockoutObservable<number>;
    countPrefix: KnockoutComputed<string>;
    sizeClass: KnockoutComputed<string>;

    constructor({ title, tooltip, route, moduleId, nav, hash, css, dynamicHash, disableWithReason, openAsDialog, itemRouteToHighlight, shardingMode, badgeData, badgeHtml, alias, requiredAccess }: {
        title: string,
        route: string | Array<string>,
        moduleId: any,
        nav: boolean | KnockoutObservable<boolean>,
        tooltip?: string,
        hash?: string,
        dynamicHash?: dynamicHashType,
        css?: string,
        openAsDialog?: boolean,
        disableWithReason?: KnockoutObservable<string>;
        shardingMode?: shardingMode;
        itemRouteToHighlight?: string;
        badgeData?: KnockoutObservable<number>;
        badgeHtml?: string;
        alias?: boolean;
        requiredAccess?: accessLevel
    }) {
        if (nav && !hash && !dynamicHash && !openAsDialog) {
            console.error("Invalid route configuration:" + title);
        }

        this.badgeHtml = badgeHtml;
        this.badgeData = badgeData || ko.observable<number>();
        this.itemRouteToHighlight = itemRouteToHighlight;
        this.title = title;
        this.tooltip = tooltip;
        this.route = route;
        this.moduleId = moduleId;
        this.nav = nav;
        this.hash = hash;
        this.dynamicHash = dynamicHash;
        this.css = css;
        this.disableWithReason = disableWithReason;
        this.shardingMode = shardingMode;
        this.openAsDialog = openAsDialog;
        this.alias = alias || false;
        this.requiredAccess = requiredAccess;

        this.path = ko.pureComputed(() => {
            if (this.hash) {
                return this.hash;
            } else if (this.dynamicHash) {
                return this.dynamicHash();
            }

            return null;
        });

        this.sizeClass = ko.pureComputed(() => {
            if (!this.badgeData) {
                return "";
            }

            return generalUtils.getSizeClass(this.badgeData());
        });

        this.countPrefix = ko.pureComputed(() => {
            if (this.badgeData != null) {
                return generalUtils.getCountPrefix(this.badgeData());
            }
            return null;
        });
    }
}

export = leafMenuItem;
